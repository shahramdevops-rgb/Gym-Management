using System.Globalization;
using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Attendances.AutoCheckout;
using Gym.Application.Cafe;
using Gym.Application.Common.Paging;
using Gym.Application.GuestDebts.ListGuestDebts;
using Gym.Application.ServiceCharges;
using Gym.Domain.Payments;
using Gym.Domain.ServiceCharges;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.GuestDebts;

/// <summary>
/// «بدهی مهمان‌ها» (BUSINESS_RULES.md §7 <i>Guest visit</i>, roadmap 6.5.31): every cafe order and
/// service charge of a guest's visit that still owes money, newest first, for both roles. Run as
/// Staff unless the test says otherwise.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class GuestDebtsEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string Path = "/api/guest-debts";

    [Fact]
    public async Task List_GuestsUnpaidChargeAndOrder_ListsBothNewestFirst()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token, "مریم احمدی");
        var order = await OrderOkAsync(client, token, visit.Id, "آب معدنی", 15_000m);
        var item = await SellOkAsync(client, token, visit.Id, "دستکش", 3, 50_000m);

        var page = await GetOkAsync<PagedResponse<GuestDebtResponse>>(client, token, Path);

        page.TotalCount.ShouldBe(2);
        page.Items.Select(debt => debt.Id).ShouldBe([item.Id, order.Id]);

        var sale = page.Items[0];
        sale.Target.ShouldBe(PaymentTargetKind.ServiceCharge);
        sale.AttendanceId.ShouldBe(visit.Id);
        sale.GuestName.ShouldBe("مریم احمدی");
        sale.ServiceKind.ShouldBe(ServiceChargeKind.Miscellaneous);
        sale.Description.ShouldBe("دستکش");
        sale.Quantity.ShouldBe(3);
        sale.CafeItems.ShouldBeNull();
        sale.Amount.ShouldBe(150_000m);
        sale.Outstanding.ShouldBe(150_000m);
        sale.PaymentStatus.ShouldBe(PaymentStatus.Unpaid);
        sale.VisitIsOpen.ShouldBeTrue();

        var cafe = page.Items[1];
        cafe.Target.ShouldBe(PaymentTargetKind.CafeOrder);
        cafe.ServiceKind.ShouldBeNull();
        cafe.CafeItems.ShouldNotBeNull().ShouldHaveSingleItem().ProductName.ShouldBe("آب معدنی");
        cafe.Outstanding.ShouldBe(15_000m);
    }

    [Fact]
    public async Task List_PartlyPaidCharge_ShowsWhatIsLeft()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);
        var cardio = await ChargeOkAsync(client, token, visit.Id, "Cardio", 30_000m);
        await PostOkAsync(client, token, $"/api/service-charges/{cardio.Id}/payments", PaymentBody(10_000m));

        var debt = (await GetOkAsync<PagedResponse<GuestDebtResponse>>(client, token, Path)).Items.ShouldHaveSingleItem();

        debt.NetPaid.ShouldBe(10_000m);
        debt.Outstanding.ShouldBe(20_000m);
        debt.PaymentStatus.ShouldBe(PaymentStatus.Partial);
    }

    [Fact]
    public async Task List_LeavesOutPaidVoidedCancelledMembersAndWalkIns()
    {
        var (client, token) = await StaffClientAsync();
        var guest = await TestGuests.CheckInOkAsync(client, token, lockerNumber: 1);
        var paid = await ChargeOkAsync(client, token, guest.Id, "Cardio", 30_000m);
        await PostOkAsync(client, token, $"/api/service-charges/{paid.Id}/payments", PaymentBody(30_000m));
        var voided = await ChargeOkAsync(client, token, guest.Id, "Analysis", 200_000m);
        await PostOkAsync(client, token, $"/api/service-charges/{voided.Id}/void", new { reason = "انجام نشد" });
        var cancelled = await OrderOkAsync(client, token, guest.Id, "چای", 10_000m);
        await PostOkAsync(client, token, $"/api/cafe/orders/{cancelled.Id}/cancel", new { reason = "اشتباه" });

        var member = await CheckedInMemberAsync(client, token, lockerNumber: 2);
        await ChargeOkAsync(client, token, member, "Cardio", 30_000m);
        await OrderOkAsync(client, token, member, "قهوه", 20_000m, memberId: await MemberOfAsync(member));

        var page = await GetOkAsync<PagedResponse<GuestDebtResponse>>(client, token, Path);

        page.Items.ShouldBeEmpty();
        page.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task List_AfterAutoCheckout_KeepsTheDebtAndMarksTheVisitClosed()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);
        await ChargeOkAsync(client, token, visit.Id, "Cardio", 30_000m);

        await using (var scope = Fixture.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AutoCheckoutHandler>().Handle(TestContext.Current.CancellationToken);
        }

        var debt = (await GetOkAsync<PagedResponse<GuestDebtResponse>>(client, token, Path)).Items.ShouldHaveSingleItem();
        debt.VisitIsOpen.ShouldBeFalse();
        debt.Outstanding.ShouldBe(30_000m);
    }

    /// <summary>Paid from the list after midnight: the charge's own endpoint, under the closed visit's lock.</summary>
    [Fact]
    public async Task List_ChargePaidAfterAutoCheckout_LeavesTheList()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);
        var cardio = await ChargeOkAsync(client, token, visit.Id, "Cardio", 30_000m);
        await using (var scope = Fixture.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AutoCheckoutHandler>().Handle(TestContext.Current.CancellationToken);
        }

        await PostOkAsync(client, token, $"/api/service-charges/{cardio.Id}/payments", PaymentBody(30_000m));

        (await GetOkAsync<PagedResponse<GuestDebtResponse>>(client, token, Path)).Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task List_Owner_Returns200()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        var client = Fixture.CreateClient();
        var token = await client.LoginForAccessTokenAsync("owner", TestUsers.Password);

        using var response = await SendAsync(client, token, HttpMethod.Get, Path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task List_PageZero_Returns400()
    {
        var (client, token) = await StaffClientAsync();

        using var response = await SendAsync(client, token, HttpMethod.Get, $"{Path}?page=0");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_NoToken_Returns401()
    {
        var client = Fixture.CreateClient();

        using var response = await client.GetAsync(Path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Helpers ----

    private static object PaymentBody(decimal amount) =>
        new { amount = amount.ToString(CultureInfo.InvariantCulture), method = "Cash", referenceNumber = (string?)null };

    private async Task<Guid> CheckedInMemberAsync(HttpClient client, string token, int lockerNumber)
    {
        var member = TestMembers.Seed("رضا احمدی", "+989160000001");
        await using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Members.Add(member);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var plan = await TestPlans.AddAsync(Fixture);
        await PostOkAsync(client, token, $"/api/members/{member.Id}/subscriptions", plan.Body);

        return (await TestLockers.CheckInOkAsync(client, token, member.Id, lockerNumber)).Id;
    }

    private async Task<Guid> MemberOfAsync(Guid attendanceId)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return db.Attendances.Single(a => a.Id == attendanceId).MemberId!.Value;
    }

    private static async Task<ServiceChargeResponse> ChargeOkAsync(
        HttpClient client, string token, Guid attendanceId, string kind, decimal amount)
    {
        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/attendance/{attendanceId}/service-charges", new { kind, amount });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await ReadAsync<ServiceChargeResponse>(response);
    }

    private static async Task<ServiceChargeResponse> SellOkAsync(
        HttpClient client, string token, Guid attendanceId, string description, int quantity, decimal unitPrice)
    {
        using var response = await SendAsync(
            client,
            token,
            HttpMethod.Post,
            $"/api/attendance/{attendanceId}/service-charges/shop",
            new { items = new[] { new { description, quantity, unitPrice } } });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await ReadAsync<List<ServiceChargeResponse>>(response)).ShouldHaveSingleItem();
    }

    /// <summary>One product, ordered on the visit and left unpaid; on a member's visit it names the member.</summary>
    private static async Task<CafeOrderResponse> OrderOkAsync(
        HttpClient client, string token, Guid attendanceId, string productName, decimal price, Guid? memberId = null)
    {
        var category = await PostOkAsync<ProductCategoryResponse>(
            client, token, "/api/cafe/categories", new { name = $"دسته {productName}" });
        var product = await PostOkAsync<ProductResponse>(
            client,
            token,
            "/api/cafe/products",
            new { name = productName, categoryId = category.Id, price = price.ToString(CultureInfo.InvariantCulture) });

        using var response = await SendAsync(
            client,
            token,
            HttpMethod.Post,
            "/api/cafe/orders",
            new
            {
                memberId,
                attendanceId,
                items = new[] { new { productId = product.Id, quantity = 1 } },
                payment = (object?)null,
            });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await ReadAsync<CafeOrderResponse>(response);
    }

    private async Task<(HttpClient Client, string Token)> StaffClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    private static async Task PostOkAsync(HttpClient client, string token, string path, object? body = null)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, path, body);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<T> PostOkAsync<T>(HttpClient client, string token, string path, object body)
        where T : class
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, path, body);
        response.EnsureSuccessStatusCode();

        return await ReadAsync<T>(response);
    }

    private static async Task<T> GetOkAsync<T>(HttpClient client, string token, string path)
        where T : class
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, path);
        response.EnsureSuccessStatusCode();

        return await ReadAsync<T>(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
        where T : class =>
        (await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client, string token, HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }
}
