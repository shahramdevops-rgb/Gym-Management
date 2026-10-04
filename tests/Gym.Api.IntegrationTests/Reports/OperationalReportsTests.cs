using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common;
using Gym.Application.History.ListSales;
using Gym.Application.Reports.GetAttendanceReport;
using Gym.Application.Reports.GetMembersReport;
using Gym.Application.Reports.GetNeedsAttention;
using Gym.Application.Reports.GetSubscriptionsSnapshot;
using Gym.Domain.Members;
using Gym.Domain.Subscriptions;
using Gym.Infrastructure.Calendar;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Gym.Api.IntegrationTests.Reports;

/// <summary>
/// The operational reports' rules: BUSINESS_RULES.md §12 <i>Operational reports</i> and <i>Needs
/// attention</i> (roadmap 9.2).
/// </summary>
/// <remarks>
/// Each rule turns on how many days lie between a date and today, so the handlers run here against a
/// <see cref="FakeTimeProvider"/> pinned to Wednesday 2026-09-30, 13:30 in Tehran, and the plans and
/// visits are written straight into the tables with the dates each case needs. The endpoint tests
/// (<see cref="OperationalReportsEndpointTests"/>) check the wiring with the real clock.
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class OperationalReportsTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");

    /// <summary>A Wednesday. 10:00 UTC is 13:30 in Tehran.</summary>
    private static readonly DateOnly Today = new(2026, 9, 30);

    private static int _phoneSuffix;

    // ---- Needs attention: running out ----

    [Fact]
    public async Task NeedsAttention_RunningOut_ListsLowSessionsAndEndingSoonAtTheirEdges()
    {
        var threeLeft = await AddMemberAsync("علی سه‌جلسه");
        await AddPlanEndingAsync(threeLeft.Id, Today.AddDays(20), used: 7);
        var fourLeft = await AddMemberAsync("مریم چهارجلسه");
        await AddPlanEndingAsync(fourLeft.Id, Today.AddDays(20), used: 6);
        var fiveDays = await AddMemberAsync("رضا پنج‌روز");
        await AddPlanEndingAsync(fiveDays.Id, Today.AddDays(5));
        var sixDays = await AddMemberAsync("زهرا شش‌روز");
        await AddPlanEndingAsync(sixDays.Id, Today.AddDays(6));
        var usedUp = await AddMemberAsync("حسن تمام‌شده");
        await AddPlanEndingAsync(usedUp.Id, Today.AddDays(20), used: 10);

        var runningOut = (await NeedsAttentionAsync()).RunningOut;

        // The soonest end first, then the fewest sessions.
        runningOut.ShouldBe(
        [
            new RunningOutResponse(fiveDays.Id, fiveDays.FullName, fiveDays.PhoneNumber, 10, Today.AddDays(5)),
            new RunningOutResponse(usedUp.Id, usedUp.FullName, usedUp.PhoneNumber, 0, Today.AddDays(20)),
            new RunningOutResponse(threeLeft.Id, threeLeft.FullName, threeLeft.PhoneNumber, 3, Today.AddDays(20)),
        ]);
    }

    [Fact]
    public async Task NeedsAttention_RunningOut_LeavesOutRenewedFrozenSingleAndInactive()
    {
        var renewed = await AddMemberAsync("علی تمدیدکرده");
        await AddPlanEndingAsync(renewed.Id, Today.AddDays(2));
        await AddPlanEndingAsync(renewed.Id, Today.AddDays(32));
        var frozen = await AddMemberAsync("مریم فریز");
        await AddPlanEndingAsync(frozen.Id, Today.AddDays(2), frozenSince: Today.AddDays(-3));
        var single = await AddMemberAsync("رضا تک‌جلسه");
        await AddSingleVisitAsync(single.Id, Today);
        var inactive = await AddMemberAsync("زهرا غیرفعال", active: false);
        await AddPlanEndingAsync(inactive.Id, Today.AddDays(2));
        var cancelledRenewal = await AddMemberAsync("حسن تمدیدلغوشده");
        await AddPlanEndingAsync(cancelledRenewal.Id, Today.AddDays(2));
        await AddPlanEndingAsync(cancelledRenewal.Id, Today.AddDays(32), cancelled: true);

        var runningOut = (await NeedsAttentionAsync()).RunningOut;

        // A cancelled renewal is no renewal (§4: it covers no dates).
        runningOut.Select(row => row.MemberId).ShouldBe([cancelledRenewal.Id]);
    }

    // ---- Needs attention: left ----

    [Fact]
    public async Task NeedsAttention_Left_ListsLastPlansEndedInTheLast30DaysAtTheirEdges()
    {
        var yesterday = await AddMemberAsync("علی دیروز");
        await AddPlanEndingAsync(yesterday.Id, Today.AddDays(-1));
        var thirtyDays = await AddMemberAsync("مریم سی‌روز");
        await AddPlanEndingAsync(thirtyDays.Id, Today.AddDays(-30));
        var thirtyOneDays = await AddMemberAsync("رضا سی‌ویک‌روز");
        await AddPlanEndingAsync(thirtyOneDays.Id, Today.AddDays(-31));
        var endsToday = await AddMemberAsync("زهرا امروز");
        await AddPlanEndingAsync(endsToday.Id, Today);

        var left = (await NeedsAttentionAsync()).Left;

        // The latest first. A plan still covers its last day, so today's is not over yet.
        left.ShouldBe(
        [
            new LeftResponse(yesterday.Id, yesterday.FullName, yesterday.PhoneNumber, Today.AddDays(-1)),
            new LeftResponse(thirtyDays.Id, thirtyDays.FullName, thirtyDays.PhoneNumber, Today.AddDays(-30)),
        ]);
    }

    [Fact]
    public async Task NeedsAttention_Left_LeavesOutRenewedFrozenSingleAndInactive()
    {
        var renewed = await AddMemberAsync("علی برگشته");
        await AddPlanEndingAsync(renewed.Id, Today.AddDays(-1));
        await AddPlanEndingAsync(renewed.Id, Today.AddDays(29));
        var frozen = await AddMemberAsync("مریم فریز");
        await AddPlanEndingAsync(frozen.Id, Today.AddDays(-1), frozenSince: Today.AddDays(-20));
        var single = await AddMemberAsync("رضا تک‌جلسه");
        await AddSingleVisitAsync(single.Id, Today.AddDays(-1));
        var inactive = await AddMemberAsync("زهرا غیرفعال", active: false);
        await AddPlanEndingAsync(inactive.Id, Today.AddDays(-1));
        var cancelledLast = await AddMemberAsync("حسن آخری‌لغوشده");
        await AddPlanEndingAsync(cancelledLast.Id, Today.AddDays(-1));
        await AddPlanEndingAsync(cancelledLast.Id, Today.AddDays(29), cancelled: true);

        var left = (await NeedsAttentionAsync()).Left;

        left.Select(row => row.MemberId).ShouldBe([cancelledLast.Id]);
    }

    // ---- Needs attention: absent ----

    [Fact]
    public async Task NeedsAttention_Absent_ListsLivePlansWithNoVisitFor10Days()
    {
        var tenDays = await AddMemberAsync("علی ده‌روز");
        var tenDaysPlan = await AddPlanStartingAsync(tenDays.Id, Today.AddDays(-20));
        await AddVisitAsync(tenDays.Id, tenDaysPlan, At(Today.AddDays(-10), 18));
        var nineDays = await AddMemberAsync("مریم نه‌روز");
        var nineDaysPlan = await AddPlanStartingAsync(nineDays.Id, Today.AddDays(-20));
        await AddVisitAsync(nineDays.Id, nineDaysPlan, At(Today.AddDays(-9), 18));
        var neverCame = await AddMemberAsync("رضا نیامده");
        await AddPlanStartingAsync(neverCame.Id, Today.AddDays(-12));
        var newPlan = await AddMemberAsync("زهرا پلن‌تازه");
        var newPlanId = await AddPlanStartingAsync(newPlan.Id, Today.AddDays(-5));
        await AddVisitAsync(newPlan.Id, newPlanId, At(Today.AddDays(-30), 18));
        var cancelledVisit = await AddMemberAsync("حسن ورودلغوشده");
        var cancelledVisitPlan = await AddPlanStartingAsync(cancelledVisit.Id, Today.AddDays(-20));
        await AddVisitAsync(cancelledVisit.Id, cancelledVisitPlan, At(Today.AddDays(-11), 18));
        await AddVisitAsync(cancelledVisit.Id, cancelledVisitPlan, At(Today.AddDays(-1), 18), cancelled: true);
        var frozen = await AddMemberAsync("سارا فریز");
        await AddPlanStartingAsync(frozen.Id, Today.AddDays(-20), frozenSince: Today.AddDays(-15));

        var absent = (await NeedsAttentionAsync()).Absent;

        // The longest away first. Never having come counts from the plan's start; a visit on an
        // earlier plan does too, and a cancelled check-in is no visit.
        absent.ShouldBe(
        [
            new AbsentResponse(neverCame.Id, neverCame.FullName, neverCame.PhoneNumber, null, 12),
            new AbsentResponse(cancelledVisit.Id, cancelledVisit.FullName, cancelledVisit.PhoneNumber, Today.AddDays(-11), 11),
            new AbsentResponse(tenDays.Id, tenDays.FullName, tenDays.PhoneNumber, Today.AddDays(-10), 10),
        ]);
    }

    [Fact]
    public async Task NeedsAttention_Absent_LastVisitLateInTheEveningCountsOnTheGymsDay()
    {
        var member = await AddMemberAsync("علی شب‌آمده");
        var plan = await AddPlanStartingAsync(member.Id, Today.AddDays(-20));
        // 00:10 in Tehran on the day 9 days ago: still 9 days, though it is the evening before in UTC.
        await AddVisitAsync(member.Id, plan, At(Today.AddDays(-9), 0, 10));

        (await NeedsAttentionAsync()).Absent.ShouldBeEmpty();
    }

    // ---- Subscriptions snapshot ----

    [Fact]
    public async Task Snapshot_CountsActiveFrozenAndRunningOutPlansOnly()
    {
        await AddPlanEndingAsync((await AddMemberAsync("علی فعال")).Id, Today.AddDays(20));
        await AddPlanEndingAsync((await AddMemberAsync("مریم رو به پایان")).Id, Today.AddDays(5));
        await AddPlanEndingAsync((await AddMemberAsync("رضا کم‌جلسه")).Id, Today.AddDays(20), used: 7);
        await AddPlanEndingAsync((await AddMemberAsync("زهرا تمام‌شده")).Id, Today.AddDays(20), used: 10);
        await AddPlanEndingAsync((await AddMemberAsync("حسن فریز")).Id, Today.AddDays(20), frozenSince: Today.AddDays(-2));
        await AddPlanStartingAsync((await AddMemberAsync("سارا آینده")).Id, Today.AddDays(1));
        await AddPlanEndingAsync((await AddMemberAsync("نرگس منقضی")).Id, Today.AddDays(-1));
        await AddPlanEndingAsync((await AddMemberAsync("کاوه لغوشده")).Id, Today.AddDays(20), cancelled: true);
        await AddSingleVisitAsync((await AddMemberAsync("پریا تک‌جلسه")).Id, Today);

        var snapshot = await SnapshotAsync();

        snapshot.ShouldBe(new SubscriptionsSnapshotResponse(Today, Active: 3, Frozen: 1, ExpiringSoon: 1, LowSessions: 1));
    }

    // ---- Renewal and new members ----

    [Fact]
    public async Task Members_Renewal_CountsAPlanSoldWithin30DaysOfTheEnd()
    {
        var fiftyDaysAgo = Today.AddDays(-50);
        var onTheLastDay = await AddMemberAsync("علی روز سی‌ام");
        await AddPlanEndingAsync(onTheLastDay.Id, fiftyDaysAgo);
        await AddPlanStartingAsync(onTheLastDay.Id, fiftyDaysAgo.AddDays(30), soldOn: fiftyDaysAgo.AddDays(30));
        var dayTooLate = await AddMemberAsync("مریم روز سی‌ویکم");
        await AddPlanEndingAsync(dayTooLate.Id, fiftyDaysAgo);
        await AddPlanStartingAsync(dayTooLate.Id, fiftyDaysAgo.AddDays(31), soldOn: fiftyDaysAgo.AddDays(31));
        var gone = await AddMemberAsync("رضا رفته");
        await AddPlanEndingAsync(gone.Id, fiftyDaysAgo);
        var cancelledRenewal = await AddMemberAsync("زهرا تمدیدلغوشده");
        await AddPlanEndingAsync(cancelledRenewal.Id, fiftyDaysAgo);
        await AddPlanStartingAsync(cancelledRenewal.Id, fiftyDaysAgo.AddDays(5), soldOn: fiftyDaysAgo.AddDays(5), cancelled: true);

        var queued = await AddMemberAsync("حسن از قبل خریده");
        await AddPlanEndingAsync(queued.Id, Today.AddDays(-10));
        await AddPlanStartingAsync(queued.Id, Today.AddDays(-9), soldOn: Today.AddDays(-15));
        var waiting = await AddMemberAsync("سارا هنوز وقت دارد");
        await AddPlanEndingAsync(waiting.Id, Today.AddDays(-10));

        await AddSingleVisitAsync((await AddMemberAsync("نرگس تک‌جلسه")).Id, Today.AddDays(-10));
        await AddPlanEndingAsync((await AddMemberAsync("کاوه امروز")).Id, Today);

        var report = await MembersAsync(Today.AddDays(-60), Today);

        report.Ended.ShouldBe(6);
        report.Renewed.ShouldBe(2);
        report.Waiting.ShouldBe(1);
        Day(report, fiftyDaysAgo).ShouldBe(new MembersDayResponse(fiftyDaysAgo, Ended: 4, Renewed: 1, Waiting: 0, NewMembers: 0));
        Day(report, Today.AddDays(-10)).ShouldBe(new MembersDayResponse(Today.AddDays(-10), Ended: 2, Renewed: 1, Waiting: 1, NewMembers: 0));
        Day(report, Today).Ended.ShouldBe(0);
        report.Days.Count.ShouldBe(61);
    }

    [Fact]
    public async Task Members_NewMembers_AreCountedOnTheDayTheirFirstPlanWasSold()
    {
        var newOne = await AddMemberAsync("علی تازه");
        await AddPlanEndingAsync(newOne.Id, Today.AddDays(-1), soldOn: Today.AddDays(-5));
        await AddPlanStartingAsync(newOne.Id, Today, soldOn: Today.AddDays(-1));
        var oldOne = await AddMemberAsync("مریم قدیمی");
        await AddPlanEndingAsync(oldOne.Id, Today.AddDays(-20), soldOn: Today.AddDays(-40));
        await AddPlanStartingAsync(oldOne.Id, Today.AddDays(-2), soldOn: Today.AddDays(-2));
        var afterAMistake = await AddMemberAsync("رضا اشتباه");
        await AddPlanStartingAsync(afterAMistake.Id, Today.AddDays(-8), soldOn: Today.AddDays(-8), cancelled: true);
        await AddPlanStartingAsync(afterAMistake.Id, Today.AddDays(-3), soldOn: Today.AddDays(-3));
        await AddSingleVisitAsync((await AddMemberAsync("زهرا تک‌جلسه")).Id, Today.AddDays(-2));

        var report = await MembersAsync(Today.AddDays(-10), Today);

        report.NewMembers.ShouldBe(2);
        Day(report, Today.AddDays(-5)).NewMembers.ShouldBe(1);
        Day(report, Today.AddDays(-3)).NewMembers.ShouldBe(1);
        Day(report, Today.AddDays(-2)).NewMembers.ShouldBe(0);
    }

    // ---- Attendance ----

    [Fact]
    public async Task Attendance_CountsMembersVisitsByTheGymsDayAndHour()
    {
        var first = await AddMemberAsync("علی ورزشکار");
        var firstPlan = await AddPlanStartingAsync(first.Id, Today.AddDays(-20));
        var second = await AddMemberAsync("مریم ورزشکار");
        var secondPlan = await AddPlanStartingAsync(second.Id, Today.AddDays(-20));

        await AddVisitAsync(first.Id, firstPlan, At(Today, 8, 30));
        await AddVisitAsync(first.Id, firstPlan, At(Today, 0, 10));
        await AddVisitAsync(second.Id, secondPlan, At(Today, 8, 45), cardioOnly: true);
        await AddVisitAsync(second.Id, secondPlan, At(Today.AddDays(-1), 23, 50));
        await AddVisitAsync(second.Id, secondPlan, At(Today, 9), cancelled: true);
        await AddGuestVisitAsync(At(Today, 10));
        await AddVisitAsync(first.Id, firstPlan, At(Today.AddDays(-3), 18));

        var report = await AttendanceAsync(Today.AddDays(-1), Today);

        report.Visits.ShouldBe(4);
        report.Members.ShouldBe(2);
        report.PreviousVisits.ShouldBe(1);
        report.Days.ShouldBe([new AttendanceDayResponse(Today.AddDays(-1), 1), new AttendanceDayResponse(Today, 3)]);
        report.ByWeekday.Select(row => row.Weekday).ShouldBe(
        [
            DayOfWeek.Saturday, DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday,
            DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
        ]);
        var wednesday = report.ByWeekday.Single(row => row.Weekday == DayOfWeek.Wednesday).Hours;
        wednesday.Count.ShouldBe(24);
        wednesday[0].ShouldBe(1);
        wednesday[8].ShouldBe(2);
        wednesday.Sum().ShouldBe(3);
        report.ByWeekday.Single(row => row.Weekday == DayOfWeek.Tuesday).Hours[23].ShouldBe(1);
    }

    // ---- Handlers under the pinned clock ----

    private static GymCalendar PinnedCalendar() => new(
        new FakeTimeProvider(new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero)),
        Options.Create(new GymCalendarOptions { TimeZone = "Asia/Tehran" }));

    private async Task<NeedsAttentionResponse> NeedsAttentionAsync()
    {
        var calendar = PinnedCalendar();
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        return await new GetNeedsAttentionHandler(db, calendar, new SaleRows(db, calendar))
            .Handle(TestContext.Current.CancellationToken);
    }

    private async Task<SubscriptionsSnapshotResponse> SnapshotAsync()
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        return await new GetSubscriptionsSnapshotHandler(db, PinnedCalendar()).Handle(TestContext.Current.CancellationToken);
    }

    private async Task<MembersReportResponse> MembersAsync(DateOnly from, DateOnly to)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        return await new GetMembersReportHandler(db, PinnedCalendar())
            .Handle(new GetMembersReportQuery(from, to), TestContext.Current.CancellationToken);
    }

    private async Task<AttendanceReportResponse> AttendanceAsync(DateOnly from, DateOnly to)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        return await new GetAttendanceReportHandler(db, PinnedCalendar())
            .Handle(new GetAttendanceReportQuery(from, to), TestContext.Current.CancellationToken);
    }

    private static MembersDayResponse Day(MembersReportResponse report, DateOnly date) =>
        report.Days.Single(day => day.Date == date);

    // ---- Rows written with the dates each case needs ----

    /// <summary>A moment on <paramref name="day"/> at the given time on Tehran's clock.</summary>
    private static DateTimeOffset At(DateOnly day, int hour, int minute = 0)
    {
        var local = day.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Unspecified);

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Tehran), TimeSpan.Zero);
    }

    private async Task<Member> AddMemberAsync(string fullName, bool active = true)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed(fullName, $"+98916{suffix:D7}");
        if (!active)
        {
            member.Deactivate();
        }

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    /// <summary>A 10-session, 30-day plan whose last day is <paramref name="endDate"/>.</summary>
    private Task<Guid> AddPlanEndingAsync(
        Guid memberId,
        DateOnly endDate,
        int used = 0,
        DateOnly? soldOn = null,
        DateOnly? frozenSince = null,
        bool cancelled = false) =>
            AddPlanStartingAsync(memberId, endDate.AddDays(-29), used, soldOn, frozenSince, cancelled);

    /// <summary>
    /// A 10-session, 30-day plan starting on <paramref name="startDate"/>, sold at noon on
    /// <paramref name="soldOn"/> (its start, when not given), with what has happened to it since.
    /// </summary>
    private async Task<Guid> AddPlanStartingAsync(
        Guid memberId,
        DateOnly startDate,
        int used = 0,
        DateOnly? soldOn = null,
        DateOnly? frozenSince = null,
        bool cancelled = false)
    {
        var plan = Subscription.CreateMembership(memberId, sessionCount: 10, sessionPrice: 90_000m, startDate).Value;

        return await SaveAsync(plan, used, soldOn ?? startDate, frozenSince, cancelled);
    }

    private async Task<Guid> AddSingleVisitAsync(Guid memberId, DateOnly day) =>
        await SaveAsync(Subscription.CreateSingleVisit(memberId, 150_000m, day).Value, used: 1, day, null, cancelled: false);

    private async Task<Guid> SaveAsync(
        Subscription plan, int used, DateOnly soldOn, DateOnly? frozenSince, bool cancelled)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Subscriptions.Add(plan);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var soldAt = At(soldOn, 12);
        DateTimeOffset? cancelledAt = cancelled ? soldAt : null;
        var reason = cancelled ? "انصراف" : null;
        await db.Database.ExecuteSqlAsync(
            $"""
            UPDATE subscriptions
            SET used_sessions = {used}, created_at = {soldAt}, frozen_since = {frozenSince},
                cancelled_at = {cancelledAt}, cancellation_reason = {reason}
            WHERE id = {plan.Id}
            """,
            TestContext.Current.CancellationToken);

        return plan.Id;
    }

    /// <summary>A closed visit at a chosen moment; a cancelled one closes when it was cancelled.</summary>
    private async Task AddVisitAsync(
        Guid memberId, Guid subscriptionId, DateTimeOffset checkedInAt, bool cancelled = false, bool cardioOnly = false)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var checkedOutAt = checkedInAt.AddMinutes(30);
        DateTimeOffset? cancelledAt = cancelled ? checkedOutAt : null;

        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO attendances (id, member_id, subscription_id, is_cardio_only, checked_in_at, checked_out_at, cancelled_at, created_at)
            VALUES ({Guid.CreateVersion7()}, {memberId}, {subscriptionId}, {cardioOnly}, {checkedInAt}, {checkedOutAt}, {cancelledAt}, now())
            """,
            TestContext.Current.CancellationToken);
    }

    private async Task AddGuestVisitAsync(DateTimeOffset checkedInAt)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO attendances (id, guest_name, checked_in_at, checked_out_at, created_at)
            VALUES ({Guid.CreateVersion7()}, {"مهمان"}, {checkedInAt}, {checkedInAt.AddMinutes(30)}, now())
            """,
            TestContext.Current.CancellationToken);
    }
}
