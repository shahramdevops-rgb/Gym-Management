using System.Globalization;
using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Attendances;
using Gym.Application.Attendances.ListCurrentlyInside;
using Gym.Application.Cafe;
using Gym.Application.Common.Paging;
using Gym.Application.Members.GetMemberDebt;
using Gym.Domain.Cafe;
using Gym.Domain.Members;
using Gym.Domain.Payments;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Cafe;

/// <summary>
/// A cafe purchase made while the member is inside, from their locker or from the till
/// (BUSINESS_RULES.md §8): tied to the visit like a هوازی charge, on the member's account, and
/// listed again at check-out.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class VisitCafeOrderEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string OrdersPath = "/api/cafe/orders";

    private static int _phoneSuffix;

    [Fact]
    public async Task CreateOrder_OnTheMembersOpenVisit_TiesTheOrderToTheVisit()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);

        using var response = await CreateAsync(client, token, visit.MemberId, visit.Id, water.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var order = (await response.Content.ReadFromJsonAsync<CafeOrderResponse>(
            TestContext.Current.CancellationToken)).ShouldNotBeNull();
        order.AttendanceId.ShouldBe(visit.Id);
        order.Outstanding.ShouldBe(15_000m);
    }

    [Fact]
    public async Task CreateOrder_OnAClosedVisit_Returns422()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        await PostOkAsync(client, token, $"/api/attendance/{visit.Id}/check-out");

        using var response = await CreateAsync(client, token, visit.MemberId, visit.Id, water.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("CafeOrders.VisitNotOpen");
    }

    [Fact]
    public async Task CreateOrder_OnAnotherMembersVisit_Returns422()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var someoneElse = await AddMemberAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);

        using var response = await CreateAsync(client, token, someoneElse.Id, visit.Id, water.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("CafeOrders.VisitOfAnotherMember");
    }

    [Fact]
    public async Task CreateOrder_UnknownVisit_Returns404()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);

        using var response = await CreateAsync(client, token, member.Id, Guid.CreateVersion7(), water.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.NotFound");
    }

    /// <summary>
    /// The till may be on another computer and names no visit, but a member who is inside is buying
    /// during their visit, so the order joins it and shows on their locker.
    /// </summary>
    [Fact]
    public async Task CreateOrder_FromTheTillWhileInside_TiesTheOrderToTheOpenVisit()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);

        var order = await CreateOkAsync(client, token, visit.MemberId, attendanceId: null, water.Id);

        order.AttendanceId.ShouldBe(visit.Id);
        var board = await GetOkAsync<PagedResponse<CurrentlyInsideResponse>>(
            client, token, "/api/attendance/currently-inside");
        board.Items.ShouldHaveSingleItem().CafeOrders.ShouldHaveSingleItem().Id.ShouldBe(order.Id);
    }

    [Fact]
    public async Task CreateOrder_FromTheTillAfterCheckOut_NamesNoVisit()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        await PostOkAsync(client, token, $"/api/attendance/{visit.Id}/check-out");

        var order = await CreateOkAsync(client, token, visit.MemberId, attendanceId: null, water.Id);

        order.AttendanceId.ShouldBeNull();
        order.Outstanding.ShouldBe(15_000m);
    }

    [Fact]
    public async Task CurrentlyInside_VisitThatBought_ShowsItsStandingOrdersOnly()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var kept = await CreateOkAsync(client, token, visit.MemberId, visit.Id, water.Id);
        var cancelled = await CreateOkAsync(client, token, visit.MemberId, visit.Id, water.Id);
        await PostOkAsync(client, token, $"{OrdersPath}/{cancelled.Id}/cancel", new { reason = "اشتباه" });

        var board = await GetOkAsync<PagedResponse<CurrentlyInsideResponse>>(
            client, token, "/api/attendance/currently-inside");

        // A cancelled order owes nothing and is left out, the way a voided هوازی charge is.
        var row = board.Items.ShouldHaveSingleItem();
        row.CafeOrders.ShouldHaveSingleItem().Id.ShouldBe(kept.Id);
    }

    [Fact]
    public async Task ListOrders_ByVisit_ReturnsOnlyWhatThatVisitBought()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var otherVisit = await CheckedInMemberAsync(client, token);
        var duringVisit = await CreateOkAsync(client, token, visit.MemberId, visit.Id, water.Id);
        await CreateOkAsync(client, token, otherVisit.MemberId, otherVisit.Id, water.Id);

        var page = await GetOkAsync<PagedResponse<CafeOrderResponse>>(
            client, token, $"{OrdersPath}?attendanceId={visit.Id}");

        page.Items.ShouldHaveSingleItem().Id.ShouldBe(duringVisit.Id);
    }

    // ---- Cancelling the check-in (BUSINESS_RULES.md §7 Cancel check-in, roadmap 6.5.8) ----

    [Fact]
    public async Task CancelCheckIn_NoOrderTicked_LeavesTheOrdersOnTheAccount()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var order = await CreateOkAsync(client, token, visit.MemberId, visit.Id, water.Id);

        await PostOkAsync(client, token, CancelPath(visit.Id), CancelCheckInBody.KeepPurchases);

        var stored = await StoredOrderAsync(order.Id);
        stored.CancelledAt.ShouldBeNull();
        var debt = await GetOkAsync<MemberDebtResponse>(client, token, $"/api/members/{visit.MemberId}/debt");
        debt.Items.ShouldContain(item => item.Id == order.Id && item.Outstanding == 15_000m);
    }

    /// <summary>
    /// Each order has its own tick: the ticked one is cancelled with the check-in's reason and its
    /// money goes back the way it came; the other stays sold.
    /// </summary>
    [Fact]
    public async Task CancelCheckIn_OneOrderTicked_CancelsOnlyThatOneAndRefundsIt()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var ticked = await CreateOkAsync(client, token, visit.MemberId, visit.Id, water.Id);
        var kept = await CreateOkAsync(client, token, visit.MemberId, visit.Id, water.Id);
        await PostOkAsync(client, token, $"{OrdersPath}/{ticked.Id}/payments", new { amount = 10_000m, method = "Card" });

        await PostOkAsync(client, token, CancelPath(visit.Id), CancelCheckInBody.Cancel(voidCardio: false, ticked.Id));

        var cancelled = await StoredOrderAsync(ticked.Id);
        cancelled.CancelledAt.ShouldNotBeNull();
        cancelled.CancelReason.ShouldBe("The check-in was cancelled.");
        var refund = (await StoredPaymentsAsync(ticked.Id)).Single(p => p.Kind == PaymentKind.Refund);
        refund.Amount.ShouldBe(10_000m);
        refund.Method.ShouldBe(PaymentMethod.Card);

        (await StoredOrderAsync(kept.Id)).CancelledAt.ShouldBeNull();
    }

    /// <summary>
    /// An order that is not a standing order of this visit refuses the whole cancellation: the
    /// visit stays open and nothing is cancelled, so nothing is half done.
    /// </summary>
    [Fact]
    public async Task CancelCheckIn_OrderOfAnotherVisit_Returns422AndChangesNothing()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var otherVisit = await CheckedInMemberAsync(client, token);
        var own = await CreateOkAsync(client, token, visit.MemberId, visit.Id, water.Id);
        var someoneElses = await CreateOkAsync(client, token, otherVisit.MemberId, otherVisit.Id, water.Id);

        using var response = await SendAsync(
            client, token, HttpMethod.Post, CancelPath(visit.Id), CancelCheckInBody.Cancel(voidCardio: true, own.Id, someoneElses.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.CafeOrderNotOnVisit");
        (await StoredOrderAsync(own.Id)).CancelledAt.ShouldBeNull();
        var board = await GetOkAsync<PagedResponse<CurrentlyInsideResponse>>(
            client, token, "/api/attendance/currently-inside");
        board.Items.ShouldContain(row => row.AttendanceId == visit.Id);
    }

    [Fact]
    public async Task CancelCheckIn_OrderAlreadyCancelledAtTheTill_Returns422()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var order = await CreateOkAsync(client, token, visit.MemberId, visit.Id, water.Id);
        await PostOkAsync(client, token, $"{OrdersPath}/{order.Id}/cancel", new { reason = "اشتباه" });

        using var response = await SendAsync(
            client, token, HttpMethod.Post, CancelPath(visit.Id), CancelCheckInBody.Cancel(voidCardio: false, order.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.CafeOrderNotOnVisit");
    }

    // ---- Helpers ----

    private static string CancelPath(Guid attendanceId) => $"/api/attendance/{attendanceId}/cancel";

    private async Task<CafeOrder> StoredOrderAsync(Guid id)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().CafeOrders
            .AsNoTracking()
            .SingleAsync(order => order.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task<List<Payment>> StoredPaymentsAsync(Guid cafeOrderId)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Payments
            .AsNoTracking()
            .Where(payment => payment.CafeOrderId == cafeOrderId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<(HttpClient Client, string Token)> StaffClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    /// <summary>A member with a subscription, checked in: the only state the board shows.</summary>
    private async Task<AttendanceResponse> CheckedInMemberAsync(HttpClient client, string token)
    {
        var member = await AddMemberAsync();
        var plan = await TestPlans.AddAsync(Fixture);

        await PostOkAsync(client, token, $"/api/members/{member.Id}/subscriptions", plan.Body);

        using var response = await TestLockers.CheckInAsync(client, token, member.Id);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<AttendanceResponse>(
            TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private async Task<Member> AddMemberAsync()
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed($"عضو {suffix}", $"+98915{suffix:D7}");

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    private static async Task<ProductResponse> AddProductAsync(
        HttpClient client, string token, string name, decimal price)
    {
        var category = await PostOkAsync<ProductCategoryResponse>(
            client, token, "/api/cafe/categories", new { name = $"دسته {name}" });

        return await PostOkAsync<ProductResponse>(
            client,
            token,
            "/api/cafe/products",
            new { name, categoryId = category.Id, price = price.ToString(CultureInfo.InvariantCulture) });
    }

    /// <summary>One of <paramref name="productId"/>, on the member's account with nothing paid.</summary>
    private static Task<HttpResponseMessage> CreateAsync(
        HttpClient client, string token, Guid memberId, Guid? attendanceId, Guid productId) =>
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

    private static async Task<CafeOrderResponse> CreateOkAsync(
        HttpClient client, string token, Guid memberId, Guid? attendanceId, Guid productId)
    {
        using var response = await CreateAsync(client, token, memberId, attendanceId, productId);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CafeOrderResponse>(
            TestContext.Current.CancellationToken)).ShouldNotBeNull();
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

        return (await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<T> GetOkAsync<T>(HttpClient client, string token, string path)
        where T : class
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, path, body: null);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client, string token, HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }
}
