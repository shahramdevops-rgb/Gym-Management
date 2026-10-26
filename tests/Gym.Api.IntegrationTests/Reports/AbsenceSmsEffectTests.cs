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
/// Each case turns on days between a message, a visit and today, so the handler runs against a
/// <see cref="FakeTimeProvider"/> pinned to Monday 2026-10-26, 13:30 in Tehran, and the messages and
/// visits are written straight into the tables at the moments each case needs. The endpoint's wiring
/// is in <see cref="OperationalReportsEndpointTests"/>.
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
    public async Task Effect_VisitWithin14Days_CameBackWithItsDays()
    {
        var three = await AddMemberWithPlanAsync("علی سه‌روز");
        await AddSentAsync(three.Member.Id, At(Today.AddDays(-20), 10));
        await AddVisitAsync(three, At(Today.AddDays(-17), 18));
        var fourteen = await AddMemberWithPlanAsync("مریم چهارده‌روز");
        await AddSentAsync(fourteen.Member.Id, At(Today.AddDays(-30), 10));
        await AddVisitAsync(fourteen, At(Today.AddDays(-16), 21));
        var fifteen = await AddMemberWithPlanAsync("رضا پانزده‌روز");
        await AddSentAsync(fifteen.Member.Id, At(Today.AddDays(-30), 10));
        await AddVisitAsync(fifteen, At(Today.AddDays(-15), 8));

        var effect = await EffectAsync(Today.AddDays(-30), Today);

        effect.Sent.ShouldBe(3);
        effect.CameBack.ShouldBe(2);
        effect.Waiting.ShouldBe(0);
        effect.DidNotComeBack.ShouldBe(1);
        effect.AverageDaysToComeBack.ShouldBe(8.5m);

        // Those who came back first, the latest sent first; then those who did not.
        effect.Messages.Select(message => (message.MemberId, message.Outcome, message.DaysToComeBack)).ShouldBe(
        [
            (three.Member.Id, AbsenceSmsOutcome.CameBack, (int?)3),
            (fourteen.Member.Id, AbsenceSmsOutcome.CameBack, 14),
            (fifteen.Member.Id, AbsenceSmsOutcome.DidNotComeBack, null),
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

        var effect = await EffectAsync(Today.AddDays(-20), Today);

        // A visit the same evening is day 0; one that morning came before the message.
        effect.Messages.Single(message => message.MemberId == cardio.Member.Id).DaysToComeBack.ShouldBe(0);
        effect.Messages.Where(message => message.Outcome == AbsenceSmsOutcome.DidNotComeBack)
            .Select(message => message.MemberId)
            .ShouldBe([before.Member.Id, cancelled.Member.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task Effect_14DaysNotOver_IsWaitingAndLeftOutOfTheAverage()
    {
        var recent = await AddMemberWithPlanAsync("علی تازه");
        await AddSentAsync(recent.Member.Id, At(Today.AddDays(-3), 10));
        var lastDay = await AddMemberWithPlanAsync("مریم روز آخر");
        await AddSentAsync(lastDay.Member.Id, At(Today.AddDays(-14), 10));
        var over = await AddMemberWithPlanAsync("رضا تمام‌شده");
        await AddSentAsync(over.Member.Id, At(Today.AddDays(-15), 10));

        var effect = await EffectAsync(Today.AddDays(-30), Today);

        effect.Waiting.ShouldBe(2);
        effect.DidNotComeBack.ShouldBe(1);
        effect.AverageDaysToComeBack.ShouldBeNull();
        effect.Messages.Where(message => message.Outcome == AbsenceSmsOutcome.Waiting)
            .Select(message => (message.MemberId, message.DaysLeft))
            .ShouldBe([(recent.Member.Id, (int?)11), (lastDay.Member.Id, 0)]);
    }

    [Fact]
    public async Task Effect_MessagesThatNeverReachedThePhone_AreLeftOutOfCountsAndCost()
    {
        var delivered = await AddMemberWithPlanAsync("علی رسیده");
        await AddSentAsync(delivered.Member.Id, At(Today.AddDays(-5), 10), delivery: SmsDelivery.Delivered);
        var unreported = await AddMemberWithPlanAsync("مریم بی‌گزارش");
        await AddSentAsync(unreported.Member.Id, At(Today.AddDays(-5), 10));
        var cancelledByProvider = await AddMemberWithPlanAsync("رضا لغوشده");
        await AddSentAsync(cancelledByProvider.Member.Id, At(Today.AddDays(-5), 10), delivery: SmsDelivery.Cancelled);
        var blocked = await AddMemberWithPlanAsync("زهرا مسدود");
        await AddSentAsync(blocked.Member.Id, At(Today.AddDays(-5), 10), delivery: SmsDelivery.BlockedByReceiver);
        var failed = await AddMemberWithPlanAsync("حسن ناموفق");
        await SaveAsync(Failed(failed.Member.Id, At(Today.AddDays(-5), 10)));
        var birthday = await AddMemberWithPlanAsync("سارا تولد");
        var birthdaySms = Notification.ForBirthday(birthday.Member.Id, 1405, Phone(), Text);
        birthdaySms.MarkSent(9_000, 2_700m, At(Today.AddDays(-5), 10)).IsSuccess.ShouldBeTrue();
        await SaveAsync(birthdaySms);

        var effect = await EffectAsync(Today.AddDays(-30), Today);

        effect.Messages.Select(message => message.MemberId)
            .ShouldBe([delivered.Member.Id, unreported.Member.Id], ignoreOrder: true);
        effect.Sent.ShouldBe(2);
        effect.CostToman.ShouldBe(270m);
    }

    [Fact]
    public async Task Effect_Range_IsByTheGymsDayOfSending()
    {
        var lateEvening = await AddMemberWithPlanAsync("علی دیروقت");
        await AddSentAsync(lateEvening.Member.Id, At(Today.AddDays(-10), 23, 50));
        var afterMidnight = await AddMemberWithPlanAsync("مریم بامداد");
        await AddSentAsync(afterMidnight.Member.Id, At(Today.AddDays(-9), 0, 10));

        var effect = await EffectAsync(Today.AddDays(-9), Today);

        effect.Messages.ShouldHaveSingleItem().MemberId.ShouldBe(afterMidnight.Member.Id);
        effect.Messages[0].SentOn.ShouldBe(Today.AddDays(-9));
    }

    [Fact]
    public async Task Effect_DeactivatedSinceAndTwoAbsences_EachMessageCounts()
    {
        var twice = await AddMemberWithPlanAsync("علی دوبار");
        await AddSentAsync(twice.Member.Id, At(Today.AddDays(-28), 10), absentSince: Today.AddDays(-40));
        await AddVisitAsync(twice, At(Today.AddDays(-27), 18));
        await AddSentAsync(twice.Member.Id, At(Today.AddDays(-5), 10), absentSince: Today.AddDays(-27));
        var deactivated = await AddMemberWithPlanAsync("مریم غیرفعال");
        await AddSentAsync(deactivated.Member.Id, At(Today.AddDays(-5), 10));
        await DeactivateAsync(deactivated.Member.Id);

        var effect = await EffectAsync(Today.AddDays(-30), Today);

        effect.Sent.ShouldBe(3);
        effect.CameBack.ShouldBe(1);
        effect.Waiting.ShouldBe(2);
    }

    [Fact]
    public async Task Effect_NoMessage_IsZerosWithNoAverage()
    {
        var effect = await EffectAsync(Today.AddDays(-30), Today);

        effect.Sent.ShouldBe(0);
        effect.AverageDaysToComeBack.ShouldBeNull();
        effect.CostToman.ShouldBe(0m);
        effect.Messages.ShouldBeEmpty();
    }

    // ---- The handler under the pinned clock ----

    private async Task<AbsenceSmsEffectResponse> EffectAsync(DateOnly from, DateOnly to)
    {
        var calendar = new GymCalendar(
            new FakeTimeProvider(new DateTimeOffset(2026, 10, 26, 10, 0, 0, TimeSpan.Zero)),
            Options.Create(new GymCalendarOptions { TimeZone = "Asia/Tehran" }));
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        return await new GetAbsenceSmsEffectHandler(db, calendar)
            .Handle(new GetAbsenceSmsEffectQuery(from, to), TestContext.Current.CancellationToken);
    }

    // ---- Rows written at the moments each case needs ----

    /// <summary>A moment on <paramref name="day"/> at the given time on Tehran's clock.</summary>
    private static DateTimeOffset At(DateOnly day, int hour, int minute = 0)
    {
        var local = day.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Unspecified);

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Tehran), TimeSpan.Zero);
    }

    private static string Phone() => $"+98917{Interlocked.Increment(ref _phoneSuffix):D7}";

    /// <summary>An active member with a plan that started 50 days ago, for their visits to belong to.</summary>
    private async Task<(Member Member, Guid PlanId)> AddMemberWithPlanAsync(string fullName)
    {
        var member = TestMembers.Seed(fullName, Phone());
        var plan = Subscription.CreateMembership(member.Id, sessionCount: 30, sessionPrice: 90_000m, Today.AddDays(-50)).Value;

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        db.Subscriptions.Add(plan);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
