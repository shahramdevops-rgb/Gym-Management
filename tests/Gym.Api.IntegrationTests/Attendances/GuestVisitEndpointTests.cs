using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Attendances;
using Gym.Application.Attendances.ListCurrentlyInside;
using Gym.Application.Attendances.TodayByHour;
using Gym.Application.Common.Paging;
using Gym.Application.Lockers;
using Gym.Application.Lockers.ListLockerVisitsToday;
using Gym.Application.ServiceCharges;
using Gym.Domain.Attendances;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Gym.Api.IntegrationTests.Attendances;

/// <summary>
/// <c>POST /api/attendance/guest-check-in</c> and everything else a guest's visit touches
/// (BUSINESS_RULES.md §7 <i>Guest visit</i>): a visit with a name and no member or subscription,
/// on the map, the board and the locker's history, moved and taken out like any other. Run as
/// Staff, because letting a guest in is front-desk work. Its cafe is in <c>GuestCafeEndpointTests</c>.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class GuestVisitEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task GuestCheckIn_OnAFreeLocker_Returns201WithTheNameAndNoMember()
    {
        var (client, token) = await StaffClientAsync();

        using var response = await TestGuests.CheckInAsync(client, token, "  مریم احمدی ", lockerNumber: 5);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var visit = await ReadAsync<AttendanceResponse>(response);
        visit.GuestName.ShouldBe("مریم احمدی");
        visit.MemberId.ShouldBeNull();
        visit.SubscriptionId.ShouldBeNull();
        visit.LockerNumber.ShouldBe(5);
        visit.UsesReservePlace.ShouldBeFalse();

        var stored = await StoredVisitAsync(visit.Id);
        stored.IsGuest.ShouldBeTrue();
        stored.LockerId.ShouldBe(TestLockers.IdOf(5));
    }

    [Fact]
    public async Task GuestCheckIn_NoLockerFree_TakesAReservePlace()
    {
        var (client, token) = await StaffClientAsync();
        await TestLockers.TakeOutOfServiceAllButAsync(Fixture);

        using var response = await TestGuests.CheckInAsync(client, token, "مریم احمدی", lockerNumber: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var visit = await ReadAsync<AttendanceResponse>(response);
        visit.UsesReservePlace.ShouldBeTrue();
        visit.LockerId.ShouldBeNull();
    }

    [Fact]
    public async Task GuestCheckIn_LockerTaken_Returns409AttendanceLockerTaken()
    {
        var (client, token) = await StaffClientAsync();
        await TestGuests.CheckInOkAsync(client, token, "مریم احمدی", lockerNumber: 1);

        using var response = await TestGuests.CheckInAsync(client, token, "زهرا کریمی", lockerNumber: 1);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.LockerTaken");
    }

    [Fact]
    public async Task GuestCheckIn_LockerOutOfService_Returns422LockersOutOfService()
    {
        var (client, token) = await StaffClientAsync();
        await TestLockers.TakeOutOfServiceAllButAsync(Fixture, 2);

        using var response = await TestGuests.CheckInAsync(client, token, "مریم احمدی", lockerNumber: 1);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Lockers.OutOfService");
    }

    [Fact]
    public async Task GuestCheckIn_ReservePlaceWhileALockerIsFree_Returns422LockersStillFree()
    {
        var (client, token) = await StaffClientAsync();

        using var response = await TestGuests.CheckInAsync(client, token, "مریم احمدی", lockerNumber: null);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.LockersStillFree");
    }

    [Fact]
    public async Task GuestCheckIn_EveryReservePlaceInUse_Returns422ReserveFull()
    {
        var (client, token) = await StaffClientAsync();
        await TestLockers.TakeOutOfServiceAllButAsync(Fixture);
        for (var i = 1; i <= Attendance.ReservePlaceCount; i++)
        {
            await TestGuests.CheckInOkAsync(client, token, $"مهمان {i}", lockerNumber: null);
        }

        using var response = await TestGuests.CheckInAsync(client, token, "مهمان آخر", lockerNumber: null);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.ReserveFull");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GuestCheckIn_BlankName_Returns400GuestNameRequired(string name)
    {
        var (client, token) = await StaffClientAsync();

        using var response = await TestGuests.CheckInAsync(client, token, name, lockerNumber: 1);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorCodeAsync(response, "guestName")).ShouldBe("Attendance.GuestNameRequired");
        (await OpenVisitCountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task GuestCheckIn_NameOverTheLimit_Returns400GuestNameTooLong()
    {
        var (client, token) = await StaffClientAsync();

        using var response = await TestGuests.CheckInAsync(
            client, token, new string('ن', Attendance.GuestNameMaxLength + 1), lockerNumber: 1);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorCodeAsync(response, "guestName")).ShouldBe("Attendance.GuestNameTooLong");
    }

    [Fact]
    public async Task GuestCheckIn_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.PostAsJsonAsync(
            TestGuests.GuestCheckInPath, new { guestName = "مریم احمدی", lockerId = TestLockers.IdOf(1) },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- On screen ----

    [Fact]
    public async Task ListLockers_GuestHoldsALocker_ShowsTheGuestsNameAndOccupied()
    {
        var (client, token) = await StaffClientAsync();
        await TestGuests.CheckInOkAsync(client, token, "مریم احمدی", lockerNumber: 3);

        var page = await GetOkAsync<PagedResponse<LockerResponse>>(client, token, $"/api/lockers?pageSize={PagingRules.MaxPageSize}");

        var locker = page.Items.Single(l => l.Number == 3);
        locker.IsOccupied.ShouldBeTrue();
        locker.OccupiedByGuestName.ShouldBe("مریم احمدی");
        locker.OccupiedByMemberId.ShouldBeNull();
        locker.HolderDebt.ShouldBe(0m);
    }

    [Fact]
    public async Task ListCurrentlyInside_GuestInside_ShowsTheNameAndNoSubscription()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token, "مریم احمدی", lockerNumber: 3);

        var page = await GetOkAsync<PagedResponse<CurrentlyInsideResponse>>(client, token, "/api/attendance/currently-inside");

        var row = page.Items.ShouldHaveSingleItem();
        row.AttendanceId.ShouldBe(visit.Id);
        row.GuestName.ShouldBe("مریم احمدی");
        row.MemberId.ShouldBeNull();
        row.MemberFullName.ShouldBeNull();
        row.SubscriptionId.ShouldBeNull();
        row.TotalSessions.ShouldBeNull();
        row.SubscriptionEndDate.ShouldBeNull();
        row.MemberBirthDate.ShouldBeNull();
        row.IsSingleSession.ShouldBeFalse();
        row.HasQueuedRenewal.ShouldBeFalse();
        row.LockerNumber.ShouldBe(3);
    }

    [Fact]
    public async Task LockerToday_AfterAGuestLeft_ListsTheGuestByName()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token, "مریم احمدی", lockerNumber: 3);
        await PostOkAsync(client, token, $"/api/attendance/{visit.Id}/check-out");

        var visits = await GetOkAsync<List<LockerVisitResponse>>(client, token, $"/api/lockers/{TestLockers.IdOf(3)}/today");

        var row = visits.ShouldHaveSingleItem();
        row.GuestName.ShouldBe("مریم احمدی");
        row.MemberId.ShouldBeNull();
        row.MemberFullName.ShouldBeNull();
        row.CheckedOutAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task TodayByHour_GuestCheckedIn_IsNotCounted()
    {
        // A guest visit is not counted as attendance (BUSINESS_RULES.md §7 Guest visit).
        var (client, token) = await StaffClientAsync();
        await TestGuests.CheckInOkAsync(client, token);

        var chart = await GetOkAsync<TodayByHourResponse>(client, token, "/api/attendance/today-by-hour");

        chart.Hours.Sum(hour => hour.Today).ShouldBe(0);
    }

    // ---- Everything else is an ordinary visit ----

    [Fact]
    public async Task MoveLocker_GuestVisit_TakesTheNewLocker()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token, lockerNumber: 1);

        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/attendance/{visit.Id}/move-locker", new { lockerId = TestLockers.IdOf(2) });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<AttendanceResponse>(response)).LockerNumber.ShouldBe(2);
        (await StoredVisitAsync(visit.Id)).GuestName.ShouldBe("مریم احمدی");
    }

    [Fact]
    public async Task CheckOut_GuestWithNoPurchases_ClosesTheVisitAndFreesTheLocker()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token, lockerNumber: 1);

        using var response = await SendAsync(client, token, HttpMethod.Post, $"/api/attendance/{visit.Id}/check-out");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var locker = await GetOkAsync<LockerResponse>(client, token, $"/api/lockers/{TestLockers.IdOf(1)}");
        locker.IsOccupied.ShouldBeFalse();
    }

    [Fact]
    public async Task CancelCheckIn_GuestWithNoPurchases_CancelsWithNoSessionToGiveBack()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);

        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/attendance/{visit.Id}/cancel", CancelCheckInBody.KeepPurchases);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<AttendanceResponse>(response)).CancelledAt.ShouldNotBeNull();
    }

    /// <summary>A guest may use every service since task 6.5.31: the هوازی is on the visit, on no account.</summary>
    [Fact]
    public async Task RecordServiceCharge_OnAGuestVisit_RecordsItWithNoMember()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);

        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/attendance/{visit.Id}/service-charges", new { kind = "Cardio", amount = 10_000m });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var charge = await ReadAsync<ServiceChargeResponse>(response);
        charge.MemberId.ShouldBeNull();
        charge.AttendanceId.ShouldBe(visit.Id);
        charge.CanChangeAmount.ShouldBeTrue();
    }

    [Fact]
    public async Task SetLockerOutOfService_GuestHoldsIt_Returns422AndKeepsItInService()
    {
        var (client, token) = await StaffClientAsync();
        await TestGuests.CheckInOkAsync(client, token, lockerNumber: 4);

        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/lockers/{TestLockers.IdOf(4)}/out-of-service");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Lockers.Occupied");
        (await GetOkAsync<LockerResponse>(client, token, $"/api/lockers/{TestLockers.IdOf(4)}")).IsOutOfService.ShouldBeFalse();
    }

    // ---- The database's own copy of the rule ----

    [Fact]
    public async Task Attendance_MemberAndGuestName_RejectedByACheckConstraint()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token, lockerNumber: 1);
        var memberVisit = await MemberVisitAsync(client, token, lockerNumber: 2);

        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteSqlAsync(
            $"UPDATE attendances SET member_id = '{memberVisit.MemberId}', subscription_id = '{memberVisit.SubscriptionId}' WHERE id = '{visit.Id}'"));

        exception.SqlState.ShouldBe("23514");
        exception.ConstraintName.ShouldBe(AttendanceConstraints.MemberOrGuest);
    }

    [Fact]
    public async Task Attendance_NeitherMemberNorGuestName_RejectedByACheckConstraint()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);

        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteSqlAsync($"UPDATE attendances SET guest_name = NULL WHERE id = '{visit.Id}'"));

        exception.SqlState.ShouldBe("23514");
        exception.ConstraintName.ShouldBe(AttendanceConstraints.MemberOrGuest);
    }

    [Fact]
    public async Task Attendance_GuestWithASubscription_RejectedByACheckConstraint()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token, lockerNumber: 1);
        var memberVisit = await MemberVisitAsync(client, token, lockerNumber: 2);

        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteSqlAsync(
            $"UPDATE attendances SET subscription_id = '{memberVisit.SubscriptionId}' WHERE id = '{visit.Id}'"));

        exception.SqlState.ShouldBe("23514");
        exception.ConstraintName.ShouldBe(AttendanceConstraints.SubscriptionWithMember);
    }

    [Fact]
    public async Task Attendance_BlankGuestName_RejectedByACheckConstraint()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);

        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteSqlAsync($"UPDATE attendances SET guest_name = '   ' WHERE id = '{visit.Id}'"));

        exception.SqlState.ShouldBe("23514");
        exception.ConstraintName.ShouldBe(AttendanceConstraints.GuestNameNotBlank);
    }

    // ---- Helpers ----

    private async Task<(HttpClient Client, string Token)> StaffClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    private async Task<Attendance> StoredVisitAsync(Guid id)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Attendances
            .AsNoTracking()
            .SingleAsync(a => a.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task<int> OpenVisitCountAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Attendances
            .CountAsync(a => a.CheckedOutAt == null, TestContext.Current.CancellationToken);
    }

    /// <summary>A member with a plan, checked in through the API: a member and a subscription for the SQL to name.</summary>
    private async Task<AttendanceResponse> MemberVisitAsync(HttpClient client, string token, int lockerNumber)
    {
        var member = TestMembers.Seed("رضا احمدی", "+989150000001");
        await using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Members.Add(member);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var plan = await TestPlans.AddAsync(Fixture);
        await PostOkAsync(client, token, $"/api/members/{member.Id}/subscriptions", plan.Body);

        return await TestLockers.CheckInOkAsync(client, token, member.Id, lockerNumber);
    }

    /// <summary>Raw SQL around the application, the way a bug or a hand-typed fix would.</summary>
    private async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A validation failure's code for one field, which the name step shows under its input.</summary>
    private static async Task<string?> FieldErrorCodeAsync(HttpResponseMessage response, string field)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return body.RootElement.GetProperty("errors").GetProperty(field)[0].GetProperty("code").GetString();
    }

    private static async Task PostOkAsync(HttpClient client, string token, string path, object? body = null)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, path, body);
        response.EnsureSuccessStatusCode();
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
