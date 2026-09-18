using Gym.Domain.Members;
using Gym.Domain.Plans;
using Gym.Domain.Subscriptions;

namespace Gym.Domain.Tests.Subscriptions;

/// <summary>BUSINESS_RULES.md §4: where a new subscription starts.</summary>
public sealed class SubscriptionScheduleTests
{
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

    private static Subscription Sell(DateOnly start, int? sessions = 12)
    {
        var plan = Plan.Create("پلن", 30, sessions, 900_000m).Value;

        return Subscription.Create(MemberId, plan, start).Value;
    }
}
