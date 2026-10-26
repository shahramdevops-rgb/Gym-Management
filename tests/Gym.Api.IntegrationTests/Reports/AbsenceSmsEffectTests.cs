using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common;
using Gym.Application.Reports.GetAbsenceSmsEffect;
using Gym.Domain.Members;
using Gym.Domain.Notifications;
using Gym.Domain.Subscriptions;
using Gym.Infrastructure.Calendar;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Gym.Api.IntegrationTests.Reports;

/// <summary>
/// The absence SMS's effect: BUSINESS_RULES.md §12 <i>The absence SMS's effect</i> (task 10.19).
/// </summary>
/// <remarks>
/// Each case turns on days between a message, a visit, the plan's end and today, so the handlers run
/// against a <see cref="FakeTimeProvider"/> pinned to Monday 2026-10-26, 13:30 in Tehran, and the
/// messages, plans and visits are written straight into the tables with the moments each case needs.
/// The endpoints' wiring is in <see cref="OperationalReportsEndpointTests"/>.
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class AbsenceSmsEffectTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string Text = "علی عزیز، جات توی باشگاه پاسارگاد خالیه!";

    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");

    /// <summary>A Monday. 10:00 UTC is 13:30 in Tehran.</summary>
    private static readonly DateOnly Today = new(2026, 10, 26);

    private static int _phoneSuffix;

    [Fact]
    public async Task Effect_VisitWithin5Days_CameBackWithItsDays()
    {
        var two = await AddMemberWithPlanAsync("علی دوروز");
        await AddSentAsync(two.Member.Id, At(Today.AddDays(-20), 10));
        await AddVisitAsync(two, At(Today.AddDays(-18), 18));
        var five = await AddMemberWithPlanAsync("مریم پنج‌روز");
        await AddSentAsync(five.Member.Id, At(Today.AddDays(-25), 10));
        await AddVisitAsync(five, At(Today.AddDays(-20), 21));
        var six = await AddMemberWithPlanAsync("رضا شش‌روز");
        await AddSentAsync(six.Member.Id, At(Today.AddDays(-25), 10));
        await AddVisitAsync(six, At(Today.AddDays(-19), 8));

        var effect = await EffectAsync();

        effect.Sent.ShouldBe(3);
        effect.CameBack.ShouldBe(2);
        effect.DidNotComeBack.ShouldBe(1);
        effect.Waiting.ShouldBe(0);
        effect.NotMeasurable.ShouldBe(0);
        effect.AverageDaysToComeBack.ShouldBe(3.5m);
        effect.CameBackByDay.ShouldBe([0, 0, 1, 0, 0, 1]);

        // Those who came back first, the latest sent first; then those who did not.
        effect.Messages.Select(message => (message.MemberId, message.Outcome, message.DaysToComeBack)).ShouldBe(
        [
            (two.Member.Id, AbsenceSmsOutcome.CameBack, (int?)2),
            (five.Member.Id, AbsenceSmsOutcome.CameBack, 5),
            (six.Member.Id, AbsenceSmsOutcome.DidNotComeBack, null),
        ]);
    }

    [Fact]
    public async Task Effect_OnlyAVisitAfterTheMessageNotCancelled_IsItsAnswer()
    {
        var before = await AddMemberWithPlanAsync("علی صبح همان روز");
        await AddSentAsync(before.Member.Id, At(Today.AddDays(-20), 18));
        await AddVisitAsync(before, At(Today.AddDays(-20), 9));
        var cancelled = await AddMemberWithPlanAsync("مریم ورود لغوشده");
        await AddSentAsync(cancelled.Member.Id, At(Today.AddDays(-20), 10));
        await AddVisitAsync(cancelled, At(Today.AddDays(-19), 18), cancelled: true);
        var cardio = await AddMemberWithPlanAsync("رضا هوازی");
        await AddSentAsync(cardio.Member.Id, At(Today.AddDays(-20), 10));
        await AddVisitAsync(cardio, At(Today.AddDays(-20), 19), cardioOnly: true);

        var effect = await EffectAsync();

        // A visit the same evening is day 0; one that morning came before the message.
        effect.Messages.Single(message => message.MemberId == cardio.Member.Id).DaysToComeBack.ShouldBe(0);
        effect.Messages.Where(message => message.Outcome == AbsenceSmsOutcome.DidNotComeBack)
            .Select(message => message.MemberId)
            .ShouldBe([before.Member.Id, cancelled.Member.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task Effect_5DaysNotOver_IsWaitingAndLeftOutOfTheAverage()
    {
        var recent = await AddMemberWithPlanAsync("علی تازه");
        await AddSentAsync(recent.Member.Id, At(Today.AddDays(-2), 10));
        var lastDay = await AddMemberWithPlanAsync("مریم روز آخر");
        await AddSentAsync(lastDay.Member.Id, At(Today.AddDays(-5), 10));
        var over = await AddMemberWithPlanAsync("رضا تمام‌شده");
        await AddSentAsync(over.Member.Id, At(Today.AddDays(-6), 10));

        var effect = await EffectAsync();

        effect.Waiting.ShouldBe(2);
        effect.DidNotComeBack.ShouldBe(1);
        effect.AverageDaysToComeBack.ShouldBeNull();
        effect.CostPerReturnToman.ShouldBeNull();
        effect.Messages.Where(message => message.Outcome == AbsenceSmsOutcome.Waiting)
            .Select(message => (message.MemberId, message.DaysLeft))
            .ShouldBe([(recent.Member.Id, (int?)3), (lastDay.Member.Id, 0)]);
    }

    [Fact]
    public async Task Effect_PlanEndingOrFrozenInsideTheWindow_IsNotMeasurable()
    {
        var endsInside = await AddMemberWithPlanAsync("علی پلن کوتاه", planEnd: Today.AddDays(-17));
        await AddSentAsync(endsInside.Member.Id, At(Today.AddDays(-20), 10));
        await AddVisitAsync(endsInside, At(Today.AddDays(-19), 18));
        var endsOnLastDay = await AddMemberWithPlanAsync("مریم پلن تا روز آخر", planEnd: Today.AddDays(-15));
        await AddSentAsync(endsOnLastDay.Member.Id, At(Today.AddDays(-20), 10));
        var frozen = await AddMemberWithPlanAsync("رضا فریز", frozenSince: Today.AddDays(-18));
        await AddSentAsync(frozen.Member.Id, At(Today.AddDays(-20), 10));
        var cancelledPlan = await AddMemberWithPlanAsync("زهرا پلن لغوشده", cancelled: true);
        await AddSentAsync(cancelledPlan.Member.Id, At(Today.AddDays(-20), 10));

        var effect = await EffectAsync();

        // Even a member who came is not measured when the plan ended inside the window: that is
        // known from the day of sending, so it leans the figure neither way.
        effect.Messages.Where(message => message.Outcome == AbsenceSmsOutcome.NotMeasurable)
            .Select(message => message.MemberId)
            .ShouldBe([endsInside.Member.Id, frozen.Member.Id, cancelledPlan.Member.Id], ignoreOrder: true);
        effect.Messages.Single(message => message.MemberId == endsOnLastDay.Member.Id).Outcome
            .ShouldBe(AbsenceSmsOutcome.DidNotComeBack);
        effect.Sent.ShouldBe(4);
        effect.NotMeasurable.ShouldBe(3);
    }

    [Fact]
    public async Task Effect_MessagesThatNeverReachedThePhone_AreLeftOutOfCountsAndCost()
    {
        var delivered = await AddMemberWithPlanAsync("علی رسیده");
        await AddSentAsync(delivered.Member.Id, At(Today.AddDays(-10), 10), delivery: SmsDelivery.Delivered);
        await AddVisitAsync(delivered, At(Today.AddDays(-9), 18));
        var unreported = await AddMemberWithPlanAsync("مریم بی‌گزارش");
        await AddSentAsync(unreported.Member.Id, At(Today.AddDays(-10), 10));
        var cancelledByProvider = await AddMemberWithPlanAsync("رضا لغوشده");
        await AddSentAsync(cancelledByProvider.Member.Id, At(Today.AddDays(-10), 10), delivery: SmsDelivery.Cancelled);
        var blocked = await AddMemberWithPlanAsync("زهرا مسدود");
        await AddSentAsync(blocked.Member.Id, At(Today.AddDays(-10), 10), delivery: SmsDelivery.BlockedByReceiver);
        var failed = await AddMemberWithPlanAsync("حسن ناموفق");
        await SaveAsync(Failed(failed.Member.Id, At(Today.AddDays(-10), 10)));
        var birthday = await AddMemberWithPlanAsync("سارا تولد");
        var birthdaySms = Notification.ForBirthday(birthday.Member.Id, 1405, Phone(), Text);
        birthdaySms.MarkSent(9_000, 2_700m, At(Today.AddDays(-10), 10)).IsSuccess.ShouldBeTrue();
        await SaveAsync(birthdaySms);

        var effect = await EffectAsync();

        effect.Messages.Select(message => message.MemberId)
            .ShouldBe([delivered.Member.Id, unreported.Member.Id], ignoreOrder: true);
        effect.Sent.ShouldBe(2);
        effect.CostToman.ShouldBe(270m);
        effect.CostPerReturnToman.ShouldBe(270m);
    }

    [Fact]
    public async Task Effect_DeactivatedSinceAndTwoAbsences_EachMessageCounts()
    {
        var twice = await AddMemberWithPlanAsync("علی دوبار");
        await AddSentAsync(twice.Member.Id, At(Today.AddDays(-28), 10), absentSince: Today.AddDays(-40));
        await AddVisitAsync(twice, At(Today.AddDays(-27), 18));
        await AddSentAsync(twice.Member.Id, At(Today.AddDays(-3), 10), absentSince: Today.AddDays(-27));
        var deactivated = await AddMemberWithPlanAsync("مریم غیرفعال");
        await AddSentAsync(deactivated.Member.Id, At(Today.AddDays(-3), 10));
        await DeactivateAsync(deactivated.Member.Id);

        var effect = await EffectAsync();

        effect.Sent.ShouldBe(3);
        effect.CameBack.ShouldBe(1);
        effect.Waiting.ShouldBe(2);
    }

    [Fact]
    public async Task Effect_NoMessage_IsZerosWithNoAverage()
    {
        var effect = await EffectAsync();

        effect.Sent.ShouldBe(0);
        effect.AverageDaysToComeBack.ShouldBeNull();
        effect.CameBackByDay.ShouldBe([0, 0, 0, 0, 0, 0]);
        effect.CostToman.ShouldBe(0m);
        effect.Messages.ShouldBeEmpty();
    }

    [Fact]
    public async Task Effect_EveryMessageSinceTheFirst_FromItsDay()
    {
        var old = await AddMemberWithPlanAsync("علی پارسال", planStart: Today.AddDays(-500), planEnd: Today.AddDays(30));
        await AddSentAsync(old.Member.Id, At(Today.AddDays(-400), 10));
        var recent = await AddMemberWithPlanAsync("مریم امسال");
        await AddSentAsync(recent.Member.Id, At(Today.AddDays(-2), 10));

        var effect = await EffectAsync();

        // More than a year back: every message since the first, whatever the dashboard's range.
        effect.From.ShouldBe(Today.AddDays(-400));
        effect.To.ShouldBe(Today);
        effect.Sent.ShouldBe(2);
        effect.DidNotComeBack.ShouldBe(1);
        effect.Waiting.ShouldBe(1);
    }

    [Fact]
    public async Task Effect_NoMessage_IsFromToday()
    {
        var effect = await EffectAsync();

        effect.From.ShouldBe(Today);
        effect.Sent.ShouldBe(0);
    }

    // ---- The handler under the pinned clock ----

    private async Task<AbsenceSmsEffectResponse> EffectAsync()
    {
        var calendar = new GymCalendar(
            new FakeTimeProvider(new DateTimeOffset(2026, 10, 26, 10, 0, 0, TimeSpan.Zero)),
            Options.Create(new GymCalendarOptions { TimeZone = "Asia/Tehran" }));
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        return await new GetAbsenceSmsEffectHandler(db, calendar).Handle(TestContext.Current.CancellationToken);
    }

    // ---- Rows written at the moments each case needs ----

    /// <summary>A moment on <paramref name="day"/> at the given time on Tehran's clock.</summary>
    private static DateTimeOffset At(DateOnly day, int hour, int minute = 0)
    {
        var local = day.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Unspecified);

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Tehran), TimeSpan.Zero);
    }

    private static string Phone() => $"+98917{Interlocked.Increment(ref _phoneSuffix):D7}";

    /// <summary>
    /// An active member with a plan from <paramref name="planStart"/> (50 days ago) to
    /// <paramref name="planEnd"/> (30 days from now), for their visits to belong to.
    /// </summary>
    private async Task<(Member Member, Guid PlanId)> AddMemberWithPlanAsync(
        string fullName,
        DateOnly? planStart = null,
        DateOnly? planEnd = null,
        DateOnly? frozenSince = null,
        bool cancelled = false)
    {
        var member = TestMembers.Seed(fullName, Phone());
        var start = planStart ?? Today.AddDays(-50);
        var plan = Subscription.CreateMembership(member.Id, sessionCount: 30, sessionPrice: 90_000m, start).Value;

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        db.Subscriptions.Add(plan);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var end = planEnd ?? Today.AddDays(30);
        DateTimeOffset? cancelledAt = cancelled ? At(start, 12) : null;
        var reason = cancelled ? "انصراف" : null;
        await db.Database.ExecuteSqlAsync(
            $"""
            UPDATE subscriptions
            SET end_date = {end}, frozen_since = {frozenSince}, cancelled_at = {cancelledAt}, cancellation_reason = {reason}
            WHERE id = {plan.Id}
            """,
            TestContext.Current.CancellationToken);

        return (member, plan.Id);
    }

    /// <summary>An absence SMS the provider accepted at <paramref name="sentAt"/>, for 1,350 Rial.</summary>
    private Task AddSentAsync(
        Guid memberId, DateTimeOffset sentAt, SmsDelivery? delivery = null, DateOnly? absentSince = null)
    {
        var sms = Notification.ForAbsence(memberId, absentSince ?? Today.AddDays(-60), Phone(), Text);
        sms.MarkSent(Interlocked.Increment(ref _phoneSuffix), 1_350m, sentAt).IsSuccess.ShouldBeTrue();
        if (delivery is { } reported)
        {
            sms.RecordDelivery(reported).IsSuccess.ShouldBeTrue();
        }

        return SaveAsync(sms);
    }

    private static Notification Failed(Guid memberId, DateTimeOffset at)
    {
        var sms = Notification.ForAbsence(memberId, Today.AddDays(-60), Phone(), Text);
        sms.MarkFailed(errorCode: 418, at).IsSuccess.ShouldBeTrue();

        return sms;
    }

    private async Task SaveAsync(Notification notification)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task DeactivateAsync(Guid memberId)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var member = await db.Members.SingleAsync(row => row.Id == memberId, TestContext.Current.CancellationToken);
        member.Deactivate();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A closed visit at a chosen moment; a cancelled one closes when it was cancelled.</summary>
    private async Task AddVisitAsync(
        (Member Member, Guid PlanId) holder, DateTimeOffset checkedInAt, bool cancelled = false, bool cardioOnly = false)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var checkedOutAt = checkedInAt.AddMinutes(30);
        DateTimeOffset? cancelledAt = cancelled ? checkedOutAt : null;

        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO attendances (id, member_id, subscription_id, is_cardio_only, checked_in_at, checked_out_at, cancelled_at, created_at)
            VALUES ({Guid.CreateVersion7()}, {holder.Member.Id}, {holder.PlanId}, {cardioOnly}, {checkedInAt}, {checkedOutAt}, {cancelledAt}, now())
            """,
            TestContext.Current.CancellationToken);
    }
}
