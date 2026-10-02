using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Attendances;
using Gym.Application.Common.Paging;
using Gym.Application.Lockers;
using Gym.Application.Subscriptions;
using Gym.Domain.Members;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Lockers;

/// <summary>
/// BUSINESS_RULES.md §6: the map is the desk's screen, the same for Staff and the Owner, and
/// nothing on it is Owner-only. Every action is tried here signed in as Staff, because a group
/// default that quietly made one of them Owner-only is exactly what task 6.5.1 had to undo.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class LockerMapStaffTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static int _phoneSuffix;

    [Fact]
    public async Task CheckIn_AsStaffWithAChosenLocker_Returns201()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberWithPlanAsync(client, token);

        using var response = await TestLockers.CheckInAsync(client, token, member.Id, lockerNumber: 3);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CheckIn_AsStaffOnAReservePlace_Returns201()
    {
        var (client, token) = await StaffClientAsync();
        await TestLockers.TakeOutOfServiceAllButAsync(Fixture);
        var member = await AddMemberWithPlanAsync(client, token);

        using var response = await TestLockers.CheckInOnReservePlaceAsync(client, token, member.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task MoveLocker_AsStaff_Returns200()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberWithPlanAsync(client, token);
        var visit = await TestLockers.CheckInOkAsync(client, token, member.Id, lockerNumber: 3);

        using var response = await PostAsync(client, token, $"/api/attendance/{visit.Id}/move-locker", new { lockerId = TestLockers.IdOf(4) });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SetLockerOutOfServiceAndBack_AsStaff_Returns200Both()
    {
        var (client, token) = await StaffClientAsync();

        using var taken = await PostAsync(client, token, $"/api/lockers/{TestLockers.IdOf(9)}/out-of-service");
        using var back = await PostAsync(client, token, $"/api/lockers/{TestLockers.IdOf(9)}/in-service");

        taken.StatusCode.ShouldBe(HttpStatusCode.OK);
        back.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Roadmap 6.5.5's "done when", as Staff and through the API the map calls: locker 12, a
    /// هوازی charge, a move to 40, and check-out turning 40 free again — no locker chosen at random.
    /// </summary>
    [Fact]
    public async Task LockerMap_WholeVisitAsStaff_EndsWithTheLastLockerFree()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberWithPlanAsync(client, token);

        var visit = await TestLockers.CheckInOkAsync(client, token, member.Id, lockerNumber: 12);
        visit.LockerNumber.ShouldBe(12);

        using (var charged = await PostAsync(client, token, $"/api/attendance/{visit.Id}/service-charges", new { kind = "Cardio", amount = 50_000m }))
        {
            charged.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        using (var moved = await PostAsync(client, token, $"/api/attendance/{visit.Id}/move-locker", new { lockerId = TestLockers.IdOf(40) }))
        {
            moved.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        (await LockerAsync(client, token, 12)).IsOccupied.ShouldBeFalse();
        (await LockerAsync(client, token, 40)).OccupiedByMemberId.ShouldBe(member.Id);

        using (var checkedOut = await PostAsync(client, token, $"/api/attendance/{visit.Id}/check-out"))
        {
            checkedOut.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await checkedOut.Content.ReadFromJsonAsync<AttendanceResponse>(TestContext.Current.CancellationToken))
                .ShouldNotBeNull().LockerNumber.ShouldBe(40);
        }

        (await LockerAsync(client, token, 40)).IsOccupied.ShouldBeFalse();
    }

    // ---- The debtor label (BUSINESS_RULES.md §6) ----

    [Fact]
    public async Task ListLockers_HolderOwesForTheirPlan_ReturnsTheDebtOnTheirLockerOnly()
    {
        var (client, token) = await StaffClientAsync();
        var (member, _) = await AddMemberWithSubscriptionAsync(client, token);
        await TestLockers.CheckInOkAsync(client, token, member.Id, lockerNumber: 67);

        var lockers = await ListLockersAsync(client, token);

        lockers.Single(locker => locker.Number == 67).HolderDebt.ShouldBe(900_000m);
        lockers.Where(locker => locker.Number != 67).ShouldAllBe(locker => locker.HolderDebt == 0);
    }

    [Fact]
    public async Task ListLockers_HolderPaidInFull_ReturnsNoDebtOnTheirLocker()
    {
        var (client, token) = await StaffClientAsync();
        var (member, subscriptionId) = await AddMemberWithSubscriptionAsync(client, token);
        using (var paid = await PostAsync(client, token, $"/api/subscriptions/{subscriptionId}/payments", new { amount = 900_000m, method = "Cash" }))
        {
            paid.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        await TestLockers.CheckInOkAsync(client, token, member.Id, lockerNumber: 67);

        var lockers = await ListLockersAsync(client, token);

        var held = lockers.Single(locker => locker.Number == 67);
        held.IsOccupied.ShouldBeTrue();
        held.HolderDebt.ShouldBe(0m);
    }

    // ---- Helpers ----

    private async Task<(HttpClient Client, string Token)> StaffClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    private async Task<Member> AddMemberWithPlanAsync(HttpClient client, string token) =>
        (await AddMemberWithSubscriptionAsync(client, token)).Member;

    /// <summary>A member sold the default 900,000 plan and paying nothing yet, so they owe all of it.</summary>
    private async Task<(Member Member, Guid SubscriptionId)> AddMemberWithSubscriptionAsync(HttpClient client, string token)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed("رضا احمدی", $"+98916{suffix:D7}");
        var plan = await TestPlans.AddAsync(Fixture);
        await using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Members.Add(member);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var assigned = await PostAsync(client, token, $"/api/members/{member.Id}/subscriptions", plan.Body);
        assigned.StatusCode.ShouldBe(HttpStatusCode.Created);
        var subscription = (await assigned.Content.ReadFromJsonAsync<SubscriptionResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

        return (member, subscription.Id);
    }

    private static async Task<IReadOnlyList<LockerResponse>> ListLockersAsync(HttpClient client, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/lockers?pageSize={PagingRules.MaxPageSize}");
        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PagedResponse<LockerResponse>>(TestContext.Current.CancellationToken)).ShouldNotBeNull().Items;
    }

    private static async Task<LockerResponse> LockerAsync(HttpClient client, string token, int number)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/lockers/{TestLockers.IdOf(number)}");
        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<LockerResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string token, string path, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }
}
