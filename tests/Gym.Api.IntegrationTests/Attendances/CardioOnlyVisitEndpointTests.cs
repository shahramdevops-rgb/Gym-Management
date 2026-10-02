using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Attendances;
using Gym.Application.Attendances.AutoCheckout;
using Gym.Application.Attendances.ListCurrentlyInside;
using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Application.Lockers;
using Gym.Application.Members.GetMemberDebt;
using Gym.Application.ServiceCharges;
using Gym.Domain.Members;
using Gym.Domain.Subscriptions;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Gym.Api.IntegrationTests.Attendances;

/// <summary>
/// «ورود فقط هوازی» (BUSINESS_RULES.md §7 <i>Cardio-only visit</i>): a member with a plan comes in
/// without a session being consumed, and leaves only once a هوازی amount is recorded. Every test
/// runs as Staff, the desk.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class CardioOnlyVisitEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static int _phoneSuffix;

    [Fact]
    public async Task CardioOnlyCheckIn_ActivePlan_Returns201AndConsumesNoSession()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var planId = await InsertMembershipAsync(member.Id);

        using var response = await CardioOnlyCheckInAsync(client, token, member.Id, lockerNumber: 4);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var visit = await ReadAsync(response);
        visit.IsCardioOnly.ShouldBeTrue();
        visit.MemberId.ShouldBe(member.Id);
        visit.SubscriptionId.ShouldBe(planId);
        visit.LockerNumber.ShouldBe(4);
        (await StoredSubscriptionAsync(planId)).UsedSessions.ShouldBe(0);
    }

    [Fact]
    public async Task CardioOnlyCheckIn_NoSubscription_Returns422AttendanceNoSubscription()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();

        using var response = await CardioOnlyCheckInAsync(client, token, member.Id, lockerNumber: 1);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.NoSubscription");
    }

    [Fact]
    public async Task CardioOnlyCheckIn_OnlyASingleVisitHeld_Returns422AttendanceNoSubscription()
    {
        // A single visit is not a plan (§7 Cardio-only visit): the member comes in the ordinary way.
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        await InsertSingleVisitAsync(member.Id);

        using var response = await CardioOnlyCheckInAsync(client, token, member.Id, lockerNumber: 1);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.NoSubscription");
    }

    [Fact]
    public async Task CardioOnlyCheckIn_NoSessionsLeft_Returns422SubscriptionsNoSessionsLeft()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        await InsertMembershipAsync(member.Id, usedSessions: 10);

        using var response = await CardioOnlyCheckInAsync(client, token, member.Id, lockerNumber: 1);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Subscriptions.NoSessionsLeft");
    }

    [Fact]
    public async Task CardioOnlyCheckIn_PlanExpired_Returns422SubscriptionsExpired()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var today = Today();
        await InsertMembershipAsync(member.Id, start: today.AddDays(-40), end: today.AddDays(-11));

        using var response = await CardioOnlyCheckInAsync(client, token, member.Id, lockerNumber: 1);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Subscriptions.Expired");
    }

    [Fact]
    public async Task CardioOnlyCheckIn_FrozenPlan_Returns201AndLeavesTheFreeze()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var today = Today();
        var planId = await InsertMembershipAsync(member.Id, frozenSince: today.AddDays(-3));

        using var response = await CardioOnlyCheckInAsync(client, token, member.Id, lockerNumber: 2);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await ReadAsync(response)).SubscriptionId.ShouldBe(planId);
        var stored = await StoredSubscriptionAsync(planId);
        stored.FrozenSince.ShouldBe(today.AddDays(-3));
        stored.UsedSessions.ShouldBe(0);
    }

    [Fact]
    public async Task CardioOnlyCheckIn_AlreadyInside_Returns422AttendanceAlreadyCheckedIn()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        await InsertMembershipAsync(member.Id);
        await CardioOnlyCheckInOkAsync(client, token, member.Id, lockerNumber: 1);

        using var response = await CardioOnlyCheckInAsync(client, token, member.Id, lockerNumber: 2);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.AlreadyCheckedIn");
    }

    [Fact]
    public async Task CardioOnlyCheckIn_LockerTaken_Returns409AttendanceLockerTaken()
    {
        var (client, token) = await StaffClientAsync();
        var first = await AddMemberAsync();
        var second = await AddMemberAsync();
        await InsertMembershipAsync(first.Id);
        await InsertMembershipAsync(second.Id);
        await CardioOnlyCheckInOkAsync(client, token, first.Id, lockerNumber: 5);

        using var response = await CardioOnlyCheckInAsync(client, token, second.Id, lockerNumber: 5);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.LockerTaken");
    }

    [Fact]
    public async Task CardioOnlyCheckIn_NoLockerFree_TakesAReservePlace()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        await InsertMembershipAsync(member.Id);
        await TestLockers.TakeOutOfServiceAllButAsync(Fixture);

        using var response = await CardioOnlyCheckInAsync(client, token, member.Id, lockerNumber: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var visit = await ReadAsync(response);
        visit.UsesReservePlace.ShouldBeTrue();
        visit.IsCardioOnly.ShouldBeTrue();
    }

    [Fact]
    public async Task CardioOnlyCheckIn_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/members/{Guid.CreateVersion7()}/attendance/cardio-only-check-in",
            new { lockerId = TestLockers.IdOf(1) },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CheckIn_Ordinary_IsNotCardioOnly()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        await InsertMembershipAsync(member.Id);

        var visit = await TestLockers.CheckInOkAsync(client, token, member.Id, lockerNumber: 1);

        visit.IsCardioOnly.ShouldBeFalse();
    }

    [Fact]
    public async Task CheckOut_CardioOnlyWithoutACardioAmount_Returns422AttendanceCardioChargeMissing()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        await InsertMembershipAsync(member.Id);
        var visit = await CardioOnlyCheckInOkAsync(client, token, member.Id, lockerNumber: 1);

        using var response = await CheckOutAsync(client, token, visit.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.CardioChargeMissing");
    }

    [Fact]
    public async Task CheckOut_CardioOnlyWithAnUnpaidCardioAmount_ClosesItAndLeavesTheDebt()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var planId = await InsertMembershipAsync(member.Id);
        var visit = await CardioOnlyCheckInOkAsync(client, token, member.Id, lockerNumber: 1);
        await RecordCardioOkAsync(client, token, visit.Id, 50_000m);

        using var response = await CheckOutAsync(client, token, visit.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync(response)).CheckedOutAt.ShouldNotBeNull();
        (await StoredSubscriptionAsync(planId)).UsedSessions.ShouldBe(0);
        (await DebtAsync(client, token, member.Id)).ShouldBe(950_000m);
    }

    [Fact]
    public async Task CheckOut_CardioOnlyWhoseCardioWasVoided_Returns422AttendanceCardioChargeMissing()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        await InsertMembershipAsync(member.Id);
        var visit = await CardioOnlyCheckInOkAsync(client, token, member.Id, lockerNumber: 1);
        var chargeId = await RecordCardioOkAsync(client, token, visit.Id, 50_000m);
        using (var voided = await SendAsync(client, token, HttpMethod.Post, $"/api/service-charges/{chargeId}/void", new { reason = "اشتباه" }))
        {
            voided.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var response = await CheckOutAsync(client, token, visit.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.CardioChargeMissing");
    }

    [Fact]
    public async Task CancelCheckIn_CardioOnly_ClosesItWithoutAnAmountAndGivesNoSessionBack()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var planId = await InsertMembershipAsync(member.Id, usedSessions: 3);
        var visit = await CardioOnlyCheckInOkAsync(client, token, member.Id, lockerNumber: 1);

        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/attendance/{visit.Id}/cancel", CancelCheckInBody.Cancel(voidCardio: false));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync(response)).CancelledAt.ShouldNotBeNull();
        (await StoredSubscriptionAsync(planId)).UsedSessions.ShouldBe(3);
    }

    [Fact]
    public async Task AutoCheckout_CardioOnlyWithoutAnAmount_LeavesItOpen()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        await InsertMembershipAsync(member.Id);
        var visit = await CardioOnlyCheckInOkAsync(client, token, member.Id, lockerNumber: 1);

        var closed = await RunAutoCheckoutAsync();

        closed.ShouldBe(0);
        (await StoredAttendanceAsync(visit.Id)).CheckedOutAt.ShouldBeNull();
    }

    [Fact]
    public async Task AutoCheckout_CardioOnlyWithAnAmount_ClosesIt()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        await InsertMembershipAsync(member.Id);
        var visit = await CardioOnlyCheckInOkAsync(client, token, member.Id, lockerNumber: 1);
        await RecordCardioOkAsync(client, token, visit.Id, 30_000m);

        var closed = await RunAutoCheckoutAsync();

        closed.ShouldBe(1);
        (await StoredAttendanceAsync(visit.Id)).AutoClosedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task ListLockers_CardioOnlyHolder_IsMarkedOnTheMap()
    {
        var (client, token) = await StaffClientAsync();
        var cardio = await AddMemberAsync();
        var ordinary = await AddMemberAsync();
        await InsertMembershipAsync(cardio.Id);
        await InsertMembershipAsync(ordinary.Id);
        await CardioOnlyCheckInOkAsync(client, token, cardio.Id, lockerNumber: 1);
        await TestLockers.CheckInOkAsync(client, token, ordinary.Id, lockerNumber: 2);

        using var response = await SendAsync(client, token, HttpMethod.Get, $"/api/lockers?pageSize={PagingRules.MaxPageSize}");

        var lockers = (await response.Content.ReadFromJsonAsync<PagedResponse<LockerResponse>>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        lockers.Items.Single(locker => locker.Number == 1).OccupiedOnCardioOnly.ShouldBeTrue();
        lockers.Items.Single(locker => locker.Number == 2).OccupiedOnCardioOnly.ShouldBeFalse();
        lockers.Items.Single(locker => locker.Number == 3).OccupiedOnCardioOnly.ShouldBeFalse();
    }

    [Fact]
    public async Task ListCurrentlyInside_CardioOnly_ShowsThePlanAndTheMark()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var planId = await InsertMembershipAsync(member.Id, usedSessions: 4);
        await CardioOnlyCheckInOkAsync(client, token, member.Id, lockerNumber: 1);

        using var response = await SendAsync(client, token, HttpMethod.Get, "/api/attendance/currently-inside");

        var inside = (await response.Content.ReadFromJsonAsync<PagedResponse<CurrentlyInsideResponse>>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        var row = inside.Items.ShouldHaveSingleItem();
        row.IsCardioOnly.ShouldBeTrue();
        row.SubscriptionId.ShouldBe(planId);
        row.UsedSessions.ShouldBe(4);
    }

    [Fact]
    public async Task Attendance_GuestVisitMarkedCardioOnly_RejectedByACheckConstraint()
    {
        var (client, token) = await StaffClientAsync();
        var guest = await TestGuests.CheckInOkAsync(client, token);

        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteSqlAsync($"UPDATE attendances SET is_cardio_only = true WHERE id = '{guest.Id}'"));

        exception.SqlState.ShouldBe("23514");
        exception.ConstraintName.ShouldBe(AttendanceConstraints.CardioOnlyIsMembers);
    }

    // ---- Helpers ----

    private async Task<(HttpClient Client, string Token)> StaffClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    private DateOnly Today()
    {
        using var scope = Fixture.CreateScope();

        return scope.ServiceProvider.GetRequiredService<IGymCalendar>().Today();
    }

    private async Task<Member> AddMemberAsync()
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed("سارا کریمی", $"+98917{suffix:D7}");

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    /// <summary>
    /// A 10-session plan for 900,000 written directly, active today unless the dates or a freeze say
    /// otherwise, so a test can set the plan's state in one step.
    /// </summary>
    private async Task<Guid> InsertMembershipAsync(
        Guid memberId, DateOnly? start = null, DateOnly? end = null, int usedSessions = 0, DateOnly? frozenSince = null)
    {
        var today = Today();
        var startDate = start ?? today.AddDays(-5);
        var endDate = end ?? today.AddDays(24);
        var id = Guid.CreateVersion7();
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO subscriptions (id, member_id, price, duration_days, total_sessions,
                                       start_date, end_date, used_sessions, frozen_since, total_frozen_days, created_at)
            VALUES ({id}, {memberId}, 900000, 30, 10,
                    {startDate}, {endDate}, {usedSessions}, {frozenSince}, 0, now())
            """,
            TestContext.Current.CancellationToken);

        return id;
    }

    private async Task InsertSingleVisitAsync(Guid memberId)
    {
        var today = Today();
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO subscriptions (id, member_id, price, duration_days, total_sessions, is_single_session,
                                       start_date, end_date, used_sessions, total_frozen_days, created_at)
            VALUES ({Guid.CreateVersion7()}, {memberId}, 120000, 1, 1, true,
                    {today}, {today}, 0, 0, now())
            """,
            TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> CardioOnlyCheckInAsync(HttpClient client, string token, Guid memberId, int? lockerNumber)
    {
        var lockerId = lockerNumber is { } number ? TestLockers.IdOf(number) : (Guid?)null;

        return SendAsync(client, token, HttpMethod.Post, $"/api/members/{memberId}/attendance/cardio-only-check-in", new { lockerId });
    }

    private static async Task<AttendanceResponse> CardioOnlyCheckInOkAsync(HttpClient client, string token, Guid memberId, int? lockerNumber)
    {
        using var response = await CardioOnlyCheckInAsync(client, token, memberId, lockerNumber);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await ReadAsync(response);
    }

    /// <summary>Records the visit's هوازی amount, unpaid, and returns the charge's id.</summary>
    private static async Task<Guid> RecordCardioOkAsync(HttpClient client, string token, Guid attendanceId, decimal amount)
    {
        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/attendance/{attendanceId}/service-charges", new { kind = "Cardio", amount });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var charge = (await response.Content.ReadFromJsonAsync<ServiceChargeResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

        return charge.Id;
    }

    private static Task<HttpResponseMessage> CheckOutAsync(HttpClient client, string token, Guid attendanceId) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/attendance/{attendanceId}/check-out");

    private static async Task<decimal> DebtAsync(HttpClient client, string token, Guid memberId)
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, $"/api/members/{memberId}/debt");
        response.EnsureSuccessStatusCode();

        var debt = (await response.Content.ReadFromJsonAsync<MemberDebtResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

        return debt.Total;
    }

    private async Task<int> RunAutoCheckoutAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AutoCheckoutHandler>()
            .Handle(TestContext.Current.CancellationToken);
    }

    private async Task<Subscription> StoredSubscriptionAsync(Guid id)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Subscriptions.AsNoTracking().SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task<Gym.Domain.Attendances.Attendance> StoredAttendanceAsync(Guid id)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Attendances.AsNoTracking().SingleAsync(a => a.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<AttendanceResponse> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<AttendanceResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string token, HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }
}
