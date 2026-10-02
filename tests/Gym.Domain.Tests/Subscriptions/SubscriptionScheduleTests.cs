using Gym.Domain.Members;
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
        cancelled.Cancel("انصراف عضو", Today, Now);

        SubscriptionSchedule.NextStartDate(Today, [cancelled]).ShouldBe(Today);
    }

    [Fact]
    public void NextStartDate_CancelledQueued_IsIgnored()
    {
        var current = Sell(new DateOnly(2026, 9, 1));
        var cancelledQueued = Sell(new DateOnly(2026, 10, 1));
        cancelledQueued.Cancel("اشتباه در ثبت", Today, Now);

        SubscriptionSchedule.NextStartDate(Today, [current, cancelledQueued]).ShouldBe(new DateOnly(2026, 10, 1));
    }

    [Fact]
    public void NextStartDate_LatestExhausted_ClosesItYesterdayAndStartsToday()
    {
        var exhausted = SellExhausted(new DateOnly(2026, 9, 1), usedOn: new DateOnly(2026, 9, 5));

        var start = SubscriptionSchedule.NextStartDate(Today, [exhausted]);

        start.ShouldBe(Today);
        exhausted.EndDate.ShouldBe(Today.AddDays(-1));
        exhausted.GetStatus(Today).ShouldBe(SubscriptionStatus.Expired);
    }

    [Fact]
    public void NextStartDate_ExhaustedOnItsFirstDay_EndsItTodayAndStartsTomorrow()
    {
        var exhausted = SellExhausted(Today, usedOn: Today);

        var start = SubscriptionSchedule.NextStartDate(Today, [exhausted]);

        exhausted.EndDate.ShouldBe(Today);
        start.ShouldBe(Today.AddDays(1));
    }

    [Fact]
    public void NextStartDate_ExhaustedButNotLatest_QueuesAfterTheLatest()
    {
        var exhausted = SellExhausted(new DateOnly(2026, 9, 1), usedOn: new DateOnly(2026, 9, 5));
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
        var member = Member.Create("رضا احمدی", "+989121234567", null, birthDate: new DateOnly(1991, 8, 3), today: Today).Value;
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
        var exhausted = SellExhausted(new DateOnly(2026, 9, 1), usedOn: new DateOnly(2026, 9, 5));
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
        var exhausted = SellExhausted(Today, usedOn: Today);
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
        var exhausted = SellExhausted(new DateOnly(2026, 9, 1), usedOn: new DateOnly(2026, 9, 5));

        SubscriptionSchedule.InEffectToday(Today, [exhausted]).ShouldBeNull();
        exhausted.EndDate.ShouldBe(new DateOnly(2026, 9, 30));
    }

    [Fact]
    public void InEffectToday_ExhaustedAndCancelledQueued_IsNull()
    {
        var exhausted = SellExhausted(new DateOnly(2026, 9, 1), usedOn: new DateOnly(2026, 9, 5));
        var cancelledQueued = Sell(new DateOnly(2026, 10, 1));
        cancelledQueued.Cancel("اشتباه در ثبت", Today, Now);

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
    public void FrozenToResume_NothingFrozen_IsNull()
    {
        var active = Sell(new DateOnly(2026, 9, 1));

        SubscriptionSchedule.FrozenToResume([active]).ShouldBeNull();
    }

    [Fact]
    public void FrozenToResume_FrozenMembership_ReturnsIt()
    {
        var frozen = Sell(new DateOnly(2026, 9, 1));
        frozen.Freeze(Today, MaxFreezeDays);
        var queued = Sell(new DateOnly(2026, 10, 1));

        SubscriptionSchedule.FrozenToResume([queued, frozen]).ShouldBeSameAs(frozen);
    }

    [Fact]
    public void FrozenToResume_FrozenThenCancelled_IsNull()
    {
        var cancelled = Sell(new DateOnly(2026, 9, 1));
        cancelled.Freeze(Today, MaxFreezeDays);
        cancelled.Cancel("انصراف عضو", Today, Now);

        SubscriptionSchedule.FrozenToResume([cancelled]).ShouldBeNull();
    }

    [Fact]
    public void FrozenToResume_TwoFrozen_ReturnsTheOneThatStartedFirst()
    {
        // The first frozen long enough for the queued one to start, and then that one frozen too.
        var first = Sell(new DateOnly(2026, 9, 1));
        first.Freeze(Today, MaxFreezeDays);
        var second = Sell(new DateOnly(2026, 10, 1));
        second.Freeze(new DateOnly(2026, 10, 5), MaxFreezeDays);

        SubscriptionSchedule.FrozenToResume([second, first]).ShouldBeSameAs(first);
    }

    [Fact]
    public void Unfreeze_FrozenWithAQueue_ExtendsItAndShiftsTheQueueByTheSameDays()
    {
        var frozen = Sell(new DateOnly(2026, 9, 1)); // ends 2026-09-30
        frozen.Freeze(Today, MaxFreezeDays);
        var queued = Sell(new DateOnly(2026, 10, 1)); // ends 2026-10-30
        var unfreezeDay = Today.AddDays(4);

        var unfrozen = SubscriptionSchedule.Unfreeze(frozen, unfreezeDay, MaxFreezeDays, [frozen, queued]);

        unfrozen.Value.ShouldBe(4);
        frozen.GetStatus(unfreezeDay).ShouldBe(SubscriptionStatus.Active);
        frozen.EndDate.ShouldBe(new DateOnly(2026, 10, 4));
        queued.StartDate.ShouldBe(new DateOnly(2026, 10, 5));
        queued.EndDate.ShouldBe(new DateOnly(2026, 11, 3));
    }

    [Fact]
    public void Unfreeze_SingleVisitsAndEarlierPlans_AreNotShifted()
    {
        var expired = Sell(new DateOnly(2026, 8, 1)); // ended 2026-08-30
        var frozen = Sell(new DateOnly(2026, 9, 1));
        frozen.Freeze(Today, MaxFreezeDays);
        var unfreezeDay = Today.AddDays(4);
        var singleVisit = Subscription.CreateSingleVisit(MemberId, 50_000m, unfreezeDay).Value;

        SubscriptionSchedule.Unfreeze(frozen, unfreezeDay, MaxFreezeDays, [expired, frozen, singleVisit]).IsSuccess.ShouldBeTrue();

        expired.EndDate.ShouldBe(new DateOnly(2026, 8, 30));
        singleVisit.StartDate.ShouldBe(unfreezeDay);
        singleVisit.EndDate.ShouldBe(unfreezeDay);
    }

    [Fact]
    public void Unfreeze_BeyondTheAllowance_ShiftsTheQueueByTheCappedDaysOnly()
    {
        var frozen = Sell(new DateOnly(2026, 9, 1));
        frozen.Freeze(Today, MaxFreezeDays);
        var queued = Sell(new DateOnly(2026, 10, 1));

        var unfrozen = SubscriptionSchedule.Unfreeze(frozen, Today.AddDays(40), MaxFreezeDays, [frozen, queued]);

        unfrozen.Value.ShouldBe(MaxFreezeDays);
        queued.StartDate.ShouldBe(new DateOnly(2026, 10, 1).AddDays(MaxFreezeDays));
    }

    [Fact]
    public void Unfreeze_SameDay_MovesNothing()
    {
        var frozen = Sell(new DateOnly(2026, 9, 1));
        frozen.Freeze(Today, MaxFreezeDays);
        var queued = Sell(new DateOnly(2026, 10, 1));

        SubscriptionSchedule.Unfreeze(frozen, Today, MaxFreezeDays, [frozen, queued]).Value.ShouldBe(0);

        frozen.EndDate.ShouldBe(new DateOnly(2026, 9, 30));
        queued.StartDate.ShouldBe(new DateOnly(2026, 10, 1));
    }

    [Fact]
    public void Unfreeze_NotFrozen_FailsAndShiftsNothing()
    {
        var active = Sell(new DateOnly(2026, 9, 1));
        var queued = Sell(new DateOnly(2026, 10, 1));

        var unfrozen = SubscriptionSchedule.Unfreeze(active, Today, MaxFreezeDays, [active, queued]);

        unfrozen.Error.ShouldBe(SubscriptionErrors.NotFrozen);
        queued.StartDate.ShouldBe(new DateOnly(2026, 10, 1));
    }

    [Fact]
    public void StartEarly_NotQueued_Throws()
    {
        var active = Sell(new DateOnly(2026, 9, 1));

        Should.Throw<InvalidOperationException>(() => active.StartEarly(Today));
    }

    /// <summary>A 10-session plan, which lasts 30 days.</summary>
    private static Subscription Sell(DateOnly start) =>
        Subscription.CreateMembership(MemberId, 10, 100_000m, start).Value;

    /// <summary>The smallest plan (5 sessions, 30 days), every session used on <paramref name="usedOn"/>.</summary>
    private static Subscription SellExhausted(DateOnly start, DateOnly usedOn)
    {
        var subscription = Subscription.CreateMembership(MemberId, Subscription.MinSessionCount, 100_000m, start).Value;
        for (var visit = 0; visit < Subscription.MinSessionCount; visit++)
        {
            subscription.ConsumeSession(usedOn).IsSuccess.ShouldBeTrue();
        }

        return subscription;
    }
}
