using System.Globalization;
using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Attendances;
using Gym.Application.Attendances.AutoCheckout;
using Gym.Application.Attendances.ListCurrentlyInside;
using Gym.Application.Cafe;
using Gym.Application.Common.Paging;
using Gym.Application.Lockers;
using Gym.Application.Payments.SettleMemberDebt;
using Gym.Domain.Payments;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Cafe;

/// <summary>
/// A guest's cafe (BUSINESS_RULES.md §7 <i>Guest visit</i>, §8): orders on the visit under the
/// guest's name, unpaid while they are inside, settled in one step, and blocking check-out and a
/// cancel that would leave them unpaid. Auto-checkout still closes the visit and leaves the orders
/// in the cafe's history to be dealt with. Run as Staff.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class GuestCafeEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string OrdersPath = "/api/cafe/orders";

    [Fact]
    public async Task CreateOrder_OnAGuestVisit_IsUnpaidUnderTheGuestsName()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token, "مریم احمدی");
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);

        using var response = await CreateAsync(client, token, memberId: null, visit.Id, water.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var order = await ReadAsync<CafeOrderResponse>(response);
        order.MemberId.ShouldBeNull();
        order.AttendanceId.ShouldBe(visit.Id);
        order.GuestName.ShouldBe("مریم احمدی");
        order.PaymentStatus.ShouldBe(PaymentStatus.Unpaid);
        order.Outstanding.ShouldBe(15_000m);
    }

    [Fact]
    public async Task CreateOrder_GuestVisitNamingAMember_Returns422VisitOfAnotherMember()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);
        var member = await AddMemberAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);

        using var response = await CreateAsync(client, token, member.Id, visit.Id, water.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("CafeOrders.VisitOfAnotherMember");
    }

    [Fact]
    public async Task CreateOrder_ClosedGuestVisit_Returns422VisitNotOpen()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);
        await PostOkAsync(client, token, CheckOutPath(visit.Id));
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);

        using var response = await CreateAsync(client, token, memberId: null, visit.Id, water.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("CafeOrders.VisitNotOpen");
    }

    [Fact]
    public async Task CreateOrder_WalkInWithNoVisitLeftUnpaid_IsStillRefused()
    {
        var (client, token) = await StaffClientAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);

        using var response = await CreateAsync(client, token, memberId: null, attendanceId: null, water.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("CafeOrders.WalkInMustBePaidInFull");
    }

    [Fact]
    public async Task ListLockers_GuestWithAnUnpaidOrder_HolderDebtIsWhatTheVisitOwes()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token, lockerNumber: 7);
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        await CreateOkAsync(client, token, visit.Id, water.Id);

        var page = await GetOkAsync<PagedResponse<LockerResponse>>(client, token, $"/api/lockers?pageSize={PagingRules.MaxPageSize}");

        page.Items.Single(locker => locker.Number == 7).HolderDebt.ShouldBe(15_000m);
    }

    [Fact]
    public async Task ListCurrentlyInside_GuestWithAnOrder_ShowsItOnTheVisit()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token, "مریم احمدی");
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        await CreateOkAsync(client, token, visit.Id, water.Id);

        var page = await GetOkAsync<PagedResponse<CurrentlyInsideResponse>>(client, token, "/api/attendance/currently-inside");

        var order = page.Items.ShouldHaveSingleItem().CafeOrders.ShouldHaveSingleItem();
        order.GuestName.ShouldBe("مریم احمدی");
        order.Outstanding.ShouldBe(15_000m);
    }

    // ---- Check-out and cancel ----

    [Fact]
    public async Task CheckOut_GuestWithAnUnpaidOrder_Returns422AndStaysInside()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        await CreateOkAsync(client, token, visit.Id, water.Id);

        using var response = await SendAsync(client, token, HttpMethod.Post, CheckOutPath(visit.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.GuestHasUnpaidCafe");
        (await IsOpenAsync(visit.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task CheckOut_GuestWhoseOrderWasPaidOnItsOwn_Succeeds()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var order = await CreateOkAsync(client, token, visit.Id, water.Id);
        await PostOkAsync(client, token, $"{OrdersPath}/{order.Id}/payments", PaymentBody(15_000m));

        using var response = await SendAsync(client, token, HttpMethod.Post, CheckOutPath(visit.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CancelCheckIn_GuestLeavingAnUnpaidOrderUnticked_Returns422AndStaysInside()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        await CreateOkAsync(client, token, visit.Id, water.Id);

        using var response = await SendAsync(
            client, token, HttpMethod.Post, CancelPath(visit.Id), CancelCheckInBody.KeepPurchases);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.GuestHasUnpaidCafe");
        (await IsOpenAsync(visit.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task CancelCheckIn_GuestTickingTheUnpaidOrder_CancelsTheVisitAndTheOrder()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var order = await CreateOkAsync(client, token, visit.Id, water.Id);

        using var response = await SendAsync(
            client, token, HttpMethod.Post, CancelPath(visit.Id), CancelCheckInBody.Cancel(voidCardio: false, order.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<AttendanceResponse>(response)).CancelledAt.ShouldNotBeNull();
        (await GetOkAsync<CafeOrderResponse>(client, token, $"{OrdersPath}/{order.Id}")).CancelledAt.ShouldNotBeNull();
    }

    // ---- تسویه یکجا ----

    [Fact]
    public async Task SettleGuest_TheWholeTotal_PaysEveryOrderAndThenCheckOutSucceeds()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var bar = await AddProductAsync(client, token, "پروتئین بار", 40_000m);
        var first = await CreateOkAsync(client, token, visit.Id, water.Id);
        var second = await CreateOkAsync(client, token, visit.Id, bar.Id);

        using var response = await SendAsync(client, token, HttpMethod.Post, SettlePath(visit.Id), PaymentBody(55_000m, "Card"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var settlement = await ReadAsync<SettlementResponse>(response);
        settlement.Payments.Select(payment => (payment.TargetId, payment.Amount))
            .ShouldBe([(first.Id, 15_000m), (second.Id, 40_000m)], ignoreOrder: true);
        settlement.RemainingDebt.ShouldBe(0m);
        (await GetOkAsync<CafeOrderResponse>(client, token, $"{OrdersPath}/{second.Id}")).PaymentStatus.ShouldBe(PaymentStatus.Paid);

        using var checkOut = await SendAsync(client, token, HttpMethod.Post, CheckOutPath(visit.Id));
        checkOut.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SettleGuest_AmountOtherThanWhatIsOwed_Returns409DebtChangedAndPaysNothing()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var order = await CreateOkAsync(client, token, visit.Id, water.Id);

        using var response = await SendAsync(client, token, HttpMethod.Post, SettlePath(visit.Id), PaymentBody(10_000m));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Settlements.DebtChanged");
        (await GetOkAsync<CafeOrderResponse>(client, token, $"{OrdersPath}/{order.Id}")).NetPaid.ShouldBe(0m);
    }

    [Fact]
    public async Task SettleGuest_NothingOwed_Returns409DebtChanged()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);

        using var response = await SendAsync(client, token, HttpMethod.Post, SettlePath(visit.Id), PaymentBody(15_000m));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Settlements.DebtChanged");
    }

    [Fact]
    public async Task SettleGuest_AMembersVisit_Returns422NotGuestVisit()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await TestPlans.AddAsync(Fixture);
        await PostOkAsync(client, token, $"/api/members/{member.Id}/subscriptions", plan.Body);
        var visit = await TestLockers.CheckInOkAsync(client, token, member.Id);

        using var response = await SendAsync(client, token, HttpMethod.Post, SettlePath(visit.Id), PaymentBody(15_000m));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.NotGuestVisit");
    }

    // ---- Auto-checkout ----

    [Fact]
    public async Task AutoCheckout_GuestWithAnUnpaidOrder_ClosesTheVisitAndTheOrderWaitsInTheHistory()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token, "مریم احمدی");
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var order = await CreateOkAsync(client, token, visit.Id, water.Id);

        await using (var scope = Fixture.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AutoCheckoutHandler>().Handle(TestContext.Current.CancellationToken);
        }

        (await IsOpenAsync(visit.Id)).ShouldBeFalse();
        var unpaid = await GetOkAsync<PagedResponse<CafeOrderResponse>>(client, token, $"{OrdersPath}?unpaidGuest=true");
        var row = unpaid.Items.ShouldHaveSingleItem();
        row.Id.ShouldBe(order.Id);
        row.GuestName.ShouldBe("مریم احمدی");
        row.Outstanding.ShouldBe(15_000m);
    }

    [Fact]
    public async Task ListOrders_UnpaidGuestFilter_LeavesOutPaidGuestOrdersAndMembersOrders()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var unpaid = await CreateOkAsync(client, token, visit.Id, water.Id);
        var paid = await CreateOkAsync(client, token, visit.Id, water.Id);
        await PostOkAsync(client, token, $"{OrdersPath}/{paid.Id}/payments", PaymentBody(15_000m));
        var member = await AddMemberAsync();
        using (var onAccount = await CreateAsync(client, token, member.Id, attendanceId: null, water.Id))
        {
            onAccount.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        var page = await GetOkAsync<PagedResponse<CafeOrderResponse>>(client, token, $"{OrdersPath}?unpaidGuest=true");

        page.Items.ShouldHaveSingleItem().Id.ShouldBe(unpaid.Id);
        page.TotalCount.ShouldBe(1);
    }

    // ---- Helpers ----

    private static string CheckOutPath(Guid attendanceId) => $"/api/attendance/{attendanceId}/check-out";

    private static string CancelPath(Guid attendanceId) => $"/api/attendance/{attendanceId}/cancel";

    private static string SettlePath(Guid attendanceId) => $"/api/attendance/{attendanceId}/settle-guest";

    private static object PaymentBody(decimal amount, string method = "Cash") =>
        new { amount = amount.ToString(CultureInfo.InvariantCulture), method, referenceNumber = (string?)null };

    private async Task<bool> IsOpenAsync(Guid attendanceId)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Attendances
            .AnyAsync(a => a.Id == attendanceId && a.CheckedOutAt == null, TestContext.Current.CancellationToken);
    }

    private async Task<(HttpClient Client, string Token)> StaffClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    private async Task<Domain.Members.Member> AddMemberAsync()
    {
        var member = TestMembers.Seed("رضا احمدی", "+989160000001");

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    private static async Task<ProductResponse> AddProductAsync(HttpClient client, string token, string name, decimal price)
    {
        var category = await PostOkAsync<ProductCategoryResponse>(
            client, token, "/api/cafe/categories", new { name = $"دسته {name}" });

        return await PostOkAsync<ProductResponse>(
            client,
            token,
            "/api/cafe/products",
            new { name, categoryId = category.Id, price = price.ToString(CultureInfo.InvariantCulture) });
    }

    private static Task<HttpResponseMessage> CreateAsync(
        HttpClient client, string token, Guid? memberId, Guid? attendanceId, Guid productId) =>
            SendAsync(
                client,
                token,
                HttpMethod.Post,
                OrdersPath,
                new
                {
                    memberId,
                    attendanceId,
                    items = new[] { new { productId, quantity = 1 } },
                    payment = (object?)null,
                });

    private static async Task<CafeOrderResponse> CreateOkAsync(HttpClient client, string token, Guid attendanceId, Guid productId)
    {
        using var response = await CreateAsync(client, token, memberId: null, attendanceId, productId);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await ReadAsync<CafeOrderResponse>(response);
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
