using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Attendances;
using Gym.Application.Attendances.ListCurrentlyInside;
using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Domain.Attendances;
using Gym.Domain.Common;
using Gym.Domain.Members;
using Gym.Domain.Subscriptions;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Attendances;

/// <summary>
/// The member's first visit on their birthday uses no session of their plan (BUSINESS_RULES.md §7
/// <i>Birthday visit</i>, task 10.8). Every member here is born on today's Jalali month and day,
/// unless a test says otherwise.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class BirthdayVisitEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const decimal SessionPrice = 100_000m;
    private const decimal SingleVisitPrice = 150_000m;

    private static int _phoneSuffix;

    [Fact]
    public async Task CheckIn_OnTheBirthdayWithAPlan_UsesNoSessionAndIsABirthdayVisit()
    {
        var (client, token) = await StaffClientAsync();
        var today = Today();
        var member = await AddMemberAsync(BornOn(today));
        var planId = await InsertMembershipAsync(member.Id, today.AddDays(-5), today.AddDays(24));

        var attendance = await TestLockers.CheckInOkAsync(client, token, member.Id, lockerNumber: 4);

        attendance.IsBirthdayVisit.ShouldBeTrue();
        attendance.SubscriptionId.ShouldBe(planId);
        (await StoredAsync(planId)).UsedSessions.ShouldBe(0);
        (await StoredAttendanceAsync(attendance.Id)).BirthdayYear.ShouldBe(JalaliCalendar.Parts(today).Year);
    }

    [Fact]
    public async Task CheckIn_NotTheBirthday_UsesASession()
    {
        var (client, token) = await StaffClientAsync();
        var today = Today();
        var member = await AddMemberAsync(BornOn(today.AddDays(1)));
        var planId = await InsertMembershipAsync(member.Id, today.AddDays(-5), today.AddDays(24));

        var attendance = await TestLockers.CheckInOkAsync(client, token, member.Id);

        attendance.IsBirthdayVisit.ShouldBeFalse();
        (await StoredAsync(planId)).UsedSessions.ShouldBe(1);
    }

    [Fact]
    public async Task CheckIn_SecondVisitOnTheBirthday_UsesASession()
    {
        var (client, token) = await StaffClientAsync();
        var today = Today();
        var member = await AddMemberAsync(BornOn(today));
        var planId = await InsertMembershipAsync(member.Id, today.AddDays(-5), today.AddDays(24));
        var first = await TestLockers.CheckInOkAsync(client, token, member.Id);
        await CheckOutOkAsync(client, token, first.Id);

        var second = await TestLockers.CheckInOkAsync(client, token, member.Id);

        first.IsBirthdayVisit.ShouldBeTrue();
        second.IsBirthdayVisit.ShouldBeFalse();
        (await StoredAsync(planId)).UsedSessions.ShouldBe(1);
    }

    [Fact]
    public async Task CancelCheckIn_BirthdayVisit_GivesNoSessionBackAndTheNextVisitIsFree()
    {
        var (client, token) = await StaffClientAsync();
        var today = Today();
        var member = await AddMemberAsync(BornOn(today));
        var planId = await InsertMembershipAsync(member.Id, today.AddDays(-5), today.AddDays(24), usedSessions: 3);
        var first = await TestLockers.CheckInOkAsync(client, token, member.Id);

        using (var cancel = await SendAsync(client, token, HttpMethod.Post, $"/api/attendance/{first.Id}/cancel", CancelCheckInBody.KeepPurchases))
        {
            cancel.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        (await StoredAsync(planId)).UsedSessions.ShouldBe(3);
        var next = await TestLockers.CheckInOkAsync(client, token, member.Id);
        next.IsBirthdayVisit.ShouldBeTrue();
        (await StoredAsync(planId)).UsedSessions.ShouldBe(3);
    }

    [Fact]
    public async Task CheckIn_SingleVisitSoldOnTheBirthday_UsesIt()
    {
        var (client, token) = await StaffClientAsync();
        await SetPricesAsync();
        var member = await AddMemberAsync(BornOn(Today()));

        using var response = await CheckInWithSaleAsync(client, token, member.Id, new { kind = "SingleVisit" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await ReadAsync(response)).IsBirthdayVisit.ShouldBeFalse();
        (await StoredSubscriptionsAsync(member.Id)).ShouldHaveSingleItem().UsedSessions.ShouldBe(1);
    }

    [Fact]
    public async Task CheckIn_PlanSoldAtTheLockerOnTheBirthday_IsFree()
    {
        // Decided by the developer, 1405/07/27: someone who buys a plan on their birthday comes in free.
        var (client, token) = await StaffClientAsync();
        await SetPricesAsync();
        var member = await AddMemberAsync(BornOn(Today()));

        using var response = await CheckInWithSaleAsync(client, token, member.Id, new { kind = "Membership", sessionCount = 12 });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await ReadAsync(response)).IsBirthdayVisit.ShouldBeTrue();
        var plan = (await StoredSubscriptionsAsync(member.Id)).ShouldHaveSingleItem();
        plan.IsSingleSession.ShouldBeFalse();
        plan.UsedSessions.ShouldBe(0);
    }

    [Fact]
    public async Task CheckIn_FrozenPlanOnTheBirthday_UnfreezesItAndUsesNoSession()
    {
        var (client, token) = await StaffClientAsync();
        var today = Today();
        var member = await AddMemberAsync(BornOn(today));
        var planId = await InsertMembershipAsync(member.Id, today.AddDays(-10), today.AddDays(19), frozenSince: today.AddDays(-4));

        var attendance = await TestLockers.CheckInOkAsync(client, token, member.Id);

        attendance.IsBirthdayVisit.ShouldBeTrue();
        attendance.UnfrozenDays.ShouldBe(4);
        var stored = await StoredAsync(planId);
        stored.FrozenSince.ShouldBeNull();
        stored.UsedSessions.ShouldBe(0);
    }

    [Fact]
    public async Task CheckIn_UsedUpPlanOnTheBirthday_IsRefusedAsUsual()
    {
        var (client, token) = await StaffClientAsync();
        var today = Today();
        var member = await AddMemberAsync(BornOn(today));
        await InsertMembershipAsync(member.Id, today.AddDays(-5), today.AddDays(24), usedSessions: 10);

        using var response = await TestLockers.CheckInAsync(client, token, member.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Subscriptions.NoSessionsLeft");
    }

    [Fact]
    public async Task CurrentlyInside_BirthdayVisit_IsMarked()
    {
        var (client, token) = await StaffClientAsync();
        var today = Today();
        var member = await AddMemberAsync(BornOn(today));
        await InsertMembershipAsync(member.Id, today.AddDays(-5), today.AddDays(24));
        await TestLockers.CheckInOkAsync(client, token, member.Id);

        using var response = await SendAsync(client, token, HttpMethod.Get, "/api/attendance/currently-inside");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = (await response.Content.ReadFromJsonAsync<PagedResponse<CurrentlyInsideResponse>>(
            TestContext.Current.CancellationToken)).ShouldNotBeNull();
        page.Items.ShouldHaveSingleItem().IsBirthdayVisit.ShouldBeTrue();
    }

    [Fact]
    public async Task Database_SecondBirthdayVisitInAYear_IsRefusedByTheIndex()
    {
        var today = Today();
        var member = await AddMemberAsync(BornOn(today));
        var planId = await InsertMembershipAsync(member.Id, today.AddDays(-5), today.AddDays(24));
        var year = JalaliCalendar.Parts(today).Year;
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var first = Attendance.CheckIn(member.Id, planId, TestLockers.IdOf(1), DateTimeOffset.UtcNow.AddHours(-2), year);
        first.CheckOut(DateTimeOffset.UtcNow.AddHours(-1)).IsSuccess.ShouldBeTrue();
        db.Attendances.Add(first);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        db.Attendances.Add(Attendance.CheckIn(member.Id, planId, TestLockers.IdOf(2), DateTimeOffset.UtcNow, year));
        var exception = await Should.ThrowAsync<UniqueConstraintException>(
            () => db.SaveChangesAsync(TestContext.Current.CancellationToken));

        exception.ConstraintName.ShouldBe(AttendanceConstraints.OneBirthdayVisitPerMemberAndYear);
    }

    // ---- Helpers ----

    /// <summary>Born 30 Jalali years before <paramref name="day"/>, on its month and day; in a leap year for 30 Esfand.</summary>
    private static DateOnly BornOn(DateOnly day)
    {
        var (year, month, dayOfMonth) = JalaliCalendar.Parts(day);
        var bornYear = year - 30;
        while (month == JalaliCalendar.Esfand && dayOfMonth == 30 && !JalaliCalendar.IsLeapYear(bornYear))
        {
            bornYear--;
        }

        return JalaliCalendar.ToDate(bornYear, month, dayOfMonth);
    }

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

    private Task SetPricesAsync() =>
        TestPlans.SetPricesAsync(Fixture, sessionPrice: SessionPrice, singleVisitPrice: SingleVisitPrice);

    private async Task<Member> AddMemberAsync(DateOnly birthDate)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed("مینا کریمی", $"+98915{suffix:D7}", birthDate);

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    /// <summary>A plan of 10 sessions written directly, so a frozen or used-up one is one step.</summary>
    private async Task<Guid> InsertMembershipAsync(
        Guid memberId, DateOnly start, DateOnly end, int usedSessions = 0, DateOnly? frozenSince = null)
    {
        var id = Guid.CreateVersion7();
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO subscriptions (id, member_id, price, duration_days, total_sessions,
                                       start_date, end_date, used_sessions, frozen_since, total_frozen_days, created_at)
            VALUES ({id}, {memberId}, 900000, 30, 10,
                    {start}, {end}, {usedSessions}, {frozenSince}, 0, now())
            """,
            TestContext.Current.CancellationToken);

        return id;
    }

    private async Task<Subscription> StoredAsync(Guid id)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Subscriptions
            .AsNoTracking()
            .SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task<List<Subscription>> StoredSubscriptionsAsync(Guid memberId)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Subscriptions
            .AsNoTracking()
            .Where(s => s.MemberId == memberId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Attendance> StoredAttendanceAsync(Guid id)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Attendances
            .AsNoTracking()
            .SingleAsync(a => a.Id == id, TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> CheckInWithSaleAsync(HttpClient client, string token, Guid memberId, object sale) =>
        SendAsync(
            client, token, HttpMethod.Post, $"/api/members/{memberId}/attendance/check-in",
            new { lockerId = TestLockers.IdOf(1), sale });

    private static async Task CheckOutOkAsync(HttpClient client, string token, Guid attendanceId)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, $"/api/attendance/{attendanceId}/check-out");
        response.EnsureSuccessStatusCode();
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
