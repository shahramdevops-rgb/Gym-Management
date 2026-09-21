using Gym.Domain.Members;
using Gym.Domain.Plans;
using Gym.Domain.Subscriptions;

namespace Gym.Domain.Tests.Subscriptions;

/// <summary>BUSINESS_RULES.md §4: where a new subscription starts.</summary>
public sealed class SubscriptionScheduleTests
{
    private const int MaxFreezeDays = 30;

    private static readonly Guid MemberId = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 9, 10);
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NextStartDate_NoSubscriptions_IsToday() =>
        SubscriptionSchedule.NextStartDate(Today, []).ShouldBe(Today);

    [Fact]
    public void NextStartDate_CurrentSubscription_IsTheDayAfterItEnds()
    {
        var current = Sell(new DateOnly(2026, 9, 1)); // ends 2026-09-30

        SubscriptionSchedule.NextStartDate(Today, [current]).ShouldBe(new DateOnly(2026, 10, 1));
    }

    [Fact]
    public void NextStartDate_CurrentAndQueued_IsTheDayAfterTheLatest()
    {
        var current = Sell(new DateOnly(2026, 9, 1));
        var queued = Sell(new DateOnly(2026, 10, 1)); // ends 2026-10-30

        SubscriptionSchedule.NextStartDate(Today, [queued, current]).ShouldBe(new DateOnly(2026, 10, 31));
    }

    [Fact]
    public void NextStartDate_OnlyExpired_IsToday()
    {
        var expired = Sell(new DateOnly(2026, 8, 1)); // ended 2026-08-30

        SubscriptionSchedule.NextStartDate(Today, [expired]).ShouldBe(Today);
    }

    [Fact]
    public void NextStartDate_EndsToday_IsTomorrow()
    {
        var current = Sell(new DateOnly(2026, 8, 12)); // ends 2026-09-10

        SubscriptionSchedule.NextStartDate(Today, [current]).ShouldBe(Today.AddDays(1));
    }

    [Fact]
    public void NextStartDate_CancelledCurrent_IsIgnored()
    {
        var cancelled = Sell(new DateOnly(2026, 9, 1));
        cancelled.Cancel("انصراف عضو", Now);

        SubscriptionSchedule.NextStartDate(Today, [cancelled]).ShouldBe(Today);
    }

    [Fact]
    public void NextStartDate_CancelledQueued_IsIgnored()
    {
        var current = Sell(new DateOnly(2026, 9, 1));
        var cancelledQueued = Sell(new DateOnly(2026, 10, 1));
        cancelledQueued.Cancel("اشتباه در ثبت", Now);

        SubscriptionSchedule.NextStartDate(Today, [current, cancelledQueued]).ShouldBe(new DateOnly(2026, 10, 1));
    }

    [Fact]
    public void NextStartDate_LatestExhausted_ClosesItYesterdayAndStartsToday()
    {
        var exhausted = Sell(new DateOnly(2026, 9, 1), sessions: 1);
        exhausted.ConsumeSession(new DateOnly(2026, 9, 5));

        var start = SubscriptionSchedule.NextStartDate(Today, [exhausted]);

        start.ShouldBe(Today);
        exhausted.EndDate.ShouldBe(Today.AddDays(-1));
        exhausted.GetStatus(Today).ShouldBe(SubscriptionStatus.Expired);
    }

    [Fact]
    public void NextStartDate_ExhaustedOnItsFirstDay_EndsItTodayAndStartsTomorrow()
    {
        var exhausted = Sell(Today, sessions: 1);
        exhausted.ConsumeSession(Today);

        var start = SubscriptionSchedule.NextStartDate(Today, [exhausted]);

        exhausted.EndDate.ShouldBe(Today);
        start.ShouldBe(Today.AddDays(1));
    }

    [Fact]
    public void NextStartDate_ExhaustedButNotLatest_QueuesAfterTheLatest()
    {
        var exhausted = Sell(new DateOnly(2026, 9, 1), sessions: 1);
        exhausted.ConsumeSession(new DateOnly(2026, 9, 5));
        var queued = Sell(new DateOnly(2026, 10, 1));

        var start = SubscriptionSchedule.NextStartDate(Today, [exhausted, queued]);

        start.ShouldBe(new DateOnly(2026, 10, 31));
        exhausted.EndDate.ShouldBe(new DateOnly(2026, 9, 30));
    }

    [Fact]
    public void CloseExhaustedEarly_NotExhausted_Throws()
    {
        var active = Sell(new DateOnly(2026, 9, 1));

        Should.Throw<InvalidOperationException>(() => active.CloseExhaustedEarly(Today));
    }

    [Fact]
    public void EnsureCanReceiveSubscription_InactiveMember_FailsWithMemberInactive()
    {
        var member = Member.Create("رضا احمدی", "+989121234567", null).Value;
        member.EnsureCanReceiveSubscription().IsSuccess.ShouldBeTrue();

        member.Deactivate();

        member.EnsureCanReceiveSubscription().Error.ShouldBe(MemberErrors.Inactive);
    }

    // ---- InEffectToday: which subscription a check-in may use (BUSINESS_RULES.md §4, §7) ----

    [Fact]
    public void InEffectToday_NoSubscriptions_IsNull() =>
        SubscriptionSchedule.InEffectToday(Today, []).ShouldBeNull();

    [Fact]
    public void InEffectToday_ActiveAndQueued_ReturnsTheActiveOne()
    {
        var active = Sell(new DateOnly(2026, 9, 1));   // ends 2026-09-30
        var queued = Sell(new DateOnly(2026, 10, 1));  // ends 2026-10-30, so it ends last

        var inEffect = SubscriptionSchedule.InEffectToday(Today, [queued, active]);

        inEffect.ShouldBe(active);
        queued.StartDate.ShouldBe(new DateOnly(2026, 10, 1));
    }

    [Fact]
    public void InEffectToday_ExhaustedAndQueued_PromotesTheQueuedOne()
    {
        var exhausted = Sell(new DateOnly(2026, 9, 1), sessions: 1);
        exhausted.ConsumeSession(new DateOnly(2026, 9, 5));
        var queued = Sell(new DateOnly(2026, 10, 1));

        var inEffect = SubscriptionSchedule.InEffectToday(Today, [exhausted, queued]);

        inEffect.ShouldBe(queued);
        exhausted.EndDate.ShouldBe(Today.AddDays(-1));
        queued.StartDate.ShouldBe(Today);
        // The full 30 days it was sold with, just starting earlier.
        queued.EndDate.ShouldBe(new DateOnly(2026, 10, 9));
        queued.GetStatus(Today).ShouldBe(SubscriptionStatus.Active);
    }

    [Fact]
    public void InEffectToday_ExhaustedOnItsFirstDay_DoesNotPromote()
    {
        var exhausted = Sell(Today, sessions: 1);
        exhausted.ConsumeSession(Today);
        var queued = Sell(new DateOnly(2026, 10, 1));

        var inEffect = SubscriptionSchedule.InEffectToday(Today, [exhausted, queued]);

        // It still covers today, so the queued one cannot move onto it: the member waits a day.
        inEffect.ShouldBeNull();
        exhausted.EndDate.ShouldBe(Today);
        queued.StartDate.ShouldBe(new DateOnly(2026, 10, 1));
    }

    [Fact]
    public void InEffectToday_ExhaustedWithNothingQueued_IsNull()
    {
        var exhausted = Sell(new DateOnly(2026, 9, 1), sessions: 1);
        exhausted.ConsumeSession(new DateOnly(2026, 9, 5));

        SubscriptionSchedule.InEffectToday(Today, [exhausted]).ShouldBeNull();
        exhausted.EndDate.ShouldBe(new DateOnly(2026, 9, 30));
    }

    [Fact]
    public void InEffectToday_ExhaustedAndCancelledQueued_IsNull()
    {
        var exhausted = Sell(new DateOnly(2026, 9, 1), sessions: 1);
        exhausted.ConsumeSession(new DateOnly(2026, 9, 5));
        var cancelledQueued = Sell(new DateOnly(2026, 10, 1));
        cancelledQueued.Cancel("اشتباه در ثبت", Now);

        SubscriptionSchedule.InEffectToday(Today, [exhausted, cancelledQueued]).ShouldBeNull();
    }

    [Fact]
    public void InEffectToday_FrozenOnly_IsNull()
    {
        var frozen = Sell(new DateOnly(2026, 9, 1));
        frozen.Freeze(Today, MaxFreezeDays);

        SubscriptionSchedule.InEffectToday(Today, [frozen]).ShouldBeNull();
    }

    [Fact]
    public void StartEarly_NotQueued_Throws()
    {
        var active = Sell(new DateOnly(2026, 9, 1));

        Should.Throw<InvalidOperationException>(() => active.StartEarly(Today));
    }

    private static Subscription Sell(DateOnly start, int? sessions = 12)
    {
        var plan = Plan.Create("پلن", 30, sessions, 900_000m).Value;

        return Subscription.Create(MemberId, plan, start).Value;
    }
}
