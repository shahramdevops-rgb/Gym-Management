using Gym.Domain.Common;
using Gym.Domain.Pricing;
using Gym.Domain.Subscriptions;

using Microsoft.Extensions.Time.Testing;

namespace Gym.Domain.Tests.Subscriptions;

/// <summary>
/// BUSINESS_RULES.md §3, §4. Most tests sell a 10-session plan, which lasts 30 days, starting on
/// 2026-09-01, so it ends on 2026-09-30.
/// </summary>
public sealed class SubscriptionTests
{
    private const int MaxFreezeDays = 30;

    /// <summary>The price of one session in these tests: 10 sessions cost 1,000,000.</summary>
    private const decimal SessionPrice = 100_000m;

    private static readonly Guid MemberId = Guid.CreateVersion7();
    private static readonly DateOnly Start = new(2026, 9, 1);
    private static readonly DateOnly End = new(2026, 9, 30);
    private static readonly DateOnly Mid = new(2026, 9, 10);
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 8, 0, 0, TimeSpan.Zero);

    // ---- CreateMembership (BUSINESS_RULES.md §3) ----

    [Fact]
    public void CreateMembership_ValidPlan_StoresTheNumbersSold()
    {
        var subscription = Subscription.CreateMembership(MemberId, 12, SessionPrice, Start).Value;

        subscription.MemberId.ShouldBe(MemberId);
        subscription.DurationDays.ShouldBe(45);
        subscription.TotalSessions.ShouldBe(12);
        subscription.UsedSessions.ShouldBe(0);
        subscription.RemainingSessions.ShouldBe(12);
        subscription.IsSingleSession.ShouldBeFalse();
    }

    [Theory]
    [InlineData(5, 30)]
    [InlineData(10, 30)]
    [InlineData(11, 45)]
    [InlineData(20, 45)]
    [InlineData(21, 70)]
    [InlineData(140, 70)]
    public void CreateMembership_SessionCount_DaysFollowTheTable(int sessions, int expectedDays)
    {
        var subscription = Subscription.CreateMembership(MemberId, sessions, SessionPrice, Start).Value;

        subscription.DurationDays.ShouldBe(expectedDays);
        subscription.EndDate.ShouldBe(Start.AddDays(expectedDays - 1));
    }

    [Theory]
    [InlineData(5, 500_000)]
    [InlineData(10, 1_000_000)]
    [InlineData(11, 1_100_000)] // more days, but the price is still per session
    [InlineData(140, 14_000_000)]
    public void CreateMembership_AnyPlan_PriceIsSessionsTimesTheSessionPrice(int sessions, int expected)
    {
        var subscription = Subscription.CreateMembership(MemberId, sessions, SessionPrice, Start).Value;

        subscription.Price.ShouldBe(expected);
    }

    [Fact]
    public void CreateMembership_SessionPriceWithCents_KeepsThemExactly()
    {
        var subscription = Subscription.CreateMembership(MemberId, 7, 1_000.25m, Start).Value;

        subscription.Price.ShouldBe(7_001.75m);
    }

    [Fact]
    public void CreateMembership_FreeSessions_PriceIsZero() =>
        Subscription.CreateMembership(MemberId, 12, 0m, Start).Value.Price.ShouldBe(0m);

    [Theory]
    [InlineData(4)]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-5)]
    public void CreateMembership_FewerThanFiveSessions_FailsWithSessionCountTooLow(int sessions) =>
        Subscription.CreateMembership(MemberId, sessions, SessionPrice, Start)
            .Error.ShouldBe(SubscriptionErrors.SessionCountTooLow);

    [Theory]
    [InlineData(141)]
    [InlineData(1_000)]
    public void CreateMembership_MoreThan140Sessions_FailsWithSessionCountTooHigh(int sessions) =>
        Subscription.CreateMembership(MemberId, sessions, SessionPrice, Start)
            .Error.ShouldBe(SubscriptionErrors.SessionCountTooHigh);

    [Fact]
    public void CreateMembership_SessionPriceNotSet_FailsWithSessionPriceNotSet() =>
        Subscription.CreateMembership(MemberId, 12, sessionPrice: null, Start)
            .Error.ShouldBe(PricingErrors.SessionPriceNotSet);

    [Fact]
    public void CreateMembership_PriceBeyondTheMoneyColumn_FailsWithPriceTooLarge() =>
        Subscription.CreateMembership(MemberId, 140, PriceList.MaxPrice, Start)
            .Error.ShouldBe(SubscriptionErrors.PriceTooLarge);

    [Fact]
    public void CreateMembership_ThirtyDayPlan_EndDateIsInclusive()
    {
        var subscription = Sell(sessions: 10);

        subscription.StartDate.ShouldBe(Start);
        subscription.EndDate.ShouldBe(new DateOnly(2026, 9, 30));
    }

    [Fact]
    public void DurationTable_LastRow_EndsAtTheMaximumSessionCount() =>
        Subscription.DurationTable[^1].MaxSessions.ShouldBe(Subscription.MaxSessionCount);

    // ---- CreateSingleVisit (BUSINESS_RULES.md §4 Single-session subscriptions) ----

    [Fact]
    public void CreateSingleVisit_PriceSet_IsOneSessionForToday()
    {
        var visit = Subscription.CreateSingleVisit(MemberId, 150_000m, Mid).Value;

        visit.IsSingleSession.ShouldBeTrue();
        visit.Price.ShouldBe(150_000m);
        visit.DurationDays.ShouldBe(1);
        visit.TotalSessions.ShouldBe(1);
        visit.StartDate.ShouldBe(Mid);
        visit.EndDate.ShouldBe(Mid);
        visit.GetStatus(Mid).ShouldBe(SubscriptionStatus.Active);
    }

    [Fact]
    public void CreateSingleVisit_PriceNotSet_FailsWithSingleVisitPriceNotSet() =>
        Subscription.CreateSingleVisit(MemberId, singleVisitPrice: null, Mid)
            .Error.ShouldBe(PricingErrors.SingleVisitPriceNotSet);

    // ---- Status: one per status ----

    [Fact]
    public void GetStatus_BeforeStartDate_IsUpcoming() =>
        Sell().GetStatus(Start.AddDays(-1)).ShouldBe(SubscriptionStatus.Upcoming);

    [Fact]
    public void GetStatus_OnStartDate_IsActive() =>
        Sell().GetStatus(Start).ShouldBe(SubscriptionStatus.Active);

    [Fact]
    public void GetStatus_OnEndDate_IsActive() =>
        Sell().GetStatus(End).ShouldBe(SubscriptionStatus.Active);

    [Fact]
    public void GetStatus_DayAfterEndDate_IsExpired() =>
        Sell().GetStatus(End.AddDays(1)).ShouldBe(SubscriptionStatus.Expired);

    [Fact]
    public void GetStatus_AllSessionsUsed_IsExhausted() =>
        InState("exhausted").GetStatus(Mid).ShouldBe(SubscriptionStatus.Exhausted);

    [Fact]
    public void GetStatus_WhileFrozen_IsFrozen() =>
        InState("frozen").GetStatus(Mid).ShouldBe(SubscriptionStatus.Frozen);

    [Fact]
    public void GetStatus_AfterCancel_IsCancelled() =>
        InState("cancelled").GetStatus(Mid).ShouldBe(SubscriptionStatus.Cancelled);

    // ---- Status: precedence when two apply ----

    [Fact]
    public void GetStatus_CancelledWhileFrozen_IsCancelled()
    {
        var subscription = InState("frozen");
        subscription.Cancel("انصراف عضو", Mid, Now).IsSuccess.ShouldBeTrue();

        subscription.GetStatus(Mid).ShouldBe(SubscriptionStatus.Cancelled);
    }

    [Fact]
    public void GetStatus_CancelledBeforeStart_IsCancelled()
    {
        var subscription = Sell();
        subscription.Cancel("اشتباه در ثبت", Start.AddDays(-5), Now).IsSuccess.ShouldBeTrue();

        subscription.GetStatus(Start.AddDays(-5)).ShouldBe(SubscriptionStatus.Cancelled);
    }

    [Fact]
    public void GetStatus_FrozenPastEndDate_IsFrozen() =>
        InState("frozen").GetStatus(End.AddDays(10)).ShouldBe(SubscriptionStatus.Frozen);

    [Fact]
    public void GetStatus_ExhaustedAndPastEndDate_IsExpired() =>
        InState("exhausted").GetStatus(End.AddDays(1)).ShouldBe(SubscriptionStatus.Expired);

    // ---- "Today" in the gym's time zone ----

    [Theory]
    [InlineData(20, 29, SubscriptionStatus.Active)]  // 23:59 in Tehran on the end date
    [InlineData(20, 30, SubscriptionStatus.Expired)] // 00:00 in Tehran, the next day
    public void GetStatus_AroundTehranMidnightOnTheEndDate_FollowsTheGymsCalendar(
        int utcHour, int utcMinute, SubscriptionStatus expected)
    {
        // Tehran is UTC+03:30, so the UTC date is still the 30th at both moments. Only the
        // gym's calendar tells them apart.
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 30, utcHour, utcMinute, 0, TimeSpan.Zero));

        Sell().GetStatus(GymToday(time)).ShouldBe(expected);
    }

    // ---- Sessions ----

    [Fact]
    public void ConsumeSession_Active_UsesOneSession()
    {
        var subscription = Sell(sessions: 12);

        subscription.ConsumeSession(Mid).IsSuccess.ShouldBeTrue();

        subscription.UsedSessions.ShouldBe(1);
        subscription.RemainingSessions.ShouldBe(11);
    }

    [Fact]
    public void ConsumeSession_LastSession_MakesItExhaustedAndTheNextOneFails()
    {
        var subscription = Sell(sessions: 5);
        for (var visit = 0; visit < 4; visit++)
        {
            subscription.ConsumeSession(Mid).IsSuccess.ShouldBeTrue();
        }

        subscription.ConsumeSession(Mid).IsSuccess.ShouldBeTrue();

        subscription.GetStatus(Mid).ShouldBe(SubscriptionStatus.Exhausted);
        subscription.ConsumeSession(Mid).Error.ShouldBe(SubscriptionErrors.NoSessionsLeft);
        subscription.UsedSessions.ShouldBe(5);
    }

    [Theory]
    [InlineData("upcoming", "Subscriptions.NotStarted")]
    [InlineData("expired", "Subscriptions.Expired")]
    [InlineData("exhausted", "Subscriptions.NoSessionsLeft")]
    [InlineData("frozen", "Subscriptions.Frozen")]
    [InlineData("cancelled", "Subscriptions.Cancelled")]
    public void ConsumeSession_NotActive_FailsWithTheReasonAndUsesNothing(string state, string expectedCode)
    {
        var subscription = InState(state);
        var usedBefore = subscription.UsedSessions;

        var result = subscription.ConsumeSession(TodayFor(state));

        result.Error.Code.ShouldBe(expectedCode);
        subscription.UsedSessions.ShouldBe(usedBefore);
    }

    [Fact]
    public void RestoreSession_NothingUsed_StaysAtZero()
    {
        var subscription = Sell();

        subscription.RestoreSession();

        subscription.UsedSessions.ShouldBe(0);
    }

    [Fact]
    public void RestoreSession_Exhausted_MakesItActiveAgain()
    {
        var subscription = InState("exhausted");

        subscription.RestoreSession();

        subscription.RemainingSessions.ShouldBe(1);
        subscription.GetStatus(Mid).ShouldBe(SubscriptionStatus.Active);
    }

    // ---- Freeze ----

    [Fact]
    public void Freeze_Active_IsFrozenFromToday()
    {
        var subscription = Sell();

        subscription.Freeze(Mid, MaxFreezeDays).IsSuccess.ShouldBeTrue();

        subscription.FrozenSince.ShouldBe(Mid);
        subscription.GetStatus(Mid).ShouldBe(SubscriptionStatus.Frozen);
    }

    [Theory]
    [InlineData("upcoming", "Subscriptions.NotStarted")]
    [InlineData("expired", "Subscriptions.Expired")]
    [InlineData("exhausted", "Subscriptions.NoSessionsLeft")]
    [InlineData("frozen", "Subscriptions.Frozen")]
    [InlineData("cancelled", "Subscriptions.Cancelled")]
    public void Freeze_NotActive_Fails(string state, string expectedCode)
    {
        var subscription = InState(state);

        subscription.Freeze(TodayFor(state), MaxFreezeDays).Error.Code.ShouldBe(expectedCode);
    }

    [Fact]
    public void Unfreeze_TwoDaysLater_ExtendsEndDateByTwoDays()
    {
        var subscription = Sell();
        subscription.Freeze(new DateOnly(2026, 9, 10), MaxFreezeDays);

        var result = subscription.Unfreeze(new DateOnly(2026, 9, 12), MaxFreezeDays);

        result.Value.ShouldBe(2);
        subscription.EndDate.ShouldBe(new DateOnly(2026, 10, 2));
        subscription.TotalFrozenDays.ShouldBe(2);
        subscription.FrozenSince.ShouldBeNull();
        subscription.GetStatus(new DateOnly(2026, 9, 12)).ShouldBe(SubscriptionStatus.Active);
    }

    [Fact]
    public void Unfreeze_SameDay_AddsNothing()
    {
        var subscription = Sell();
        subscription.Freeze(Mid, MaxFreezeDays);

        subscription.Unfreeze(Mid, MaxFreezeDays).Value.ShouldBe(0);

        subscription.EndDate.ShouldBe(End);
        subscription.GetStatus(Mid).ShouldBe(SubscriptionStatus.Active);
    }

    [Fact]
    public void Unfreeze_SeveralFreezes_DaysAddUp()
    {
        var subscription = Sell();
        subscription.Freeze(new DateOnly(2026, 9, 5), MaxFreezeDays);
        subscription.Unfreeze(new DateOnly(2026, 9, 8), MaxFreezeDays);
        subscription.Freeze(new DateOnly(2026, 9, 15), MaxFreezeDays);

        subscription.Unfreeze(new DateOnly(2026, 9, 19), MaxFreezeDays);

        subscription.TotalFrozenDays.ShouldBe(7);
        subscription.EndDate.ShouldBe(End.AddDays(7));
    }

    [Fact]
    public void Unfreeze_LongerThanTheDaysLeft_ExtendsOnlyByTheDaysLeft()
    {
        var subscription = Sell();
        subscription.Freeze(new DateOnly(2026, 9, 2), MaxFreezeDays);
        subscription.Unfreeze(new DateOnly(2026, 9, 27), MaxFreezeDays); // 25 of 30 days used
        subscription.Freeze(new DateOnly(2026, 9, 28), MaxFreezeDays);

        var result = subscription.Unfreeze(new DateOnly(2026, 10, 8), MaxFreezeDays); // 10 days frozen

        result.Value.ShouldBe(5);
        subscription.TotalFrozenDays.ShouldBe(MaxFreezeDays);
        subscription.EndDate.ShouldBe(End.AddDays(30));
        subscription.FrozenSince.ShouldBeNull();
    }

    [Fact]
    public void Unfreeze_FrozenPastTheEndDate_IsActiveAgainWithTheLaterEndDate()
    {
        var subscription = Sell();
        subscription.Freeze(new DateOnly(2026, 9, 25), MaxFreezeDays);

        subscription.Unfreeze(new DateOnly(2026, 10, 5), MaxFreezeDays).Value.ShouldBe(10);

        subscription.EndDate.ShouldBe(new DateOnly(2026, 10, 10));
        subscription.GetStatus(new DateOnly(2026, 10, 5)).ShouldBe(SubscriptionStatus.Active);
    }

    [Fact]
    public void Freeze_NoFreezeDaysLeft_FailsWithFreezeLimitReached()
    {
        var subscription = Sell(sessions: 21); // 70 days
        subscription.Freeze(new DateOnly(2026, 9, 2), MaxFreezeDays);
        subscription.Unfreeze(new DateOnly(2026, 10, 2), MaxFreezeDays); // all 30 used

        var result = subscription.Freeze(new DateOnly(2026, 10, 5), MaxFreezeDays);

        result.Error.ShouldBe(SubscriptionErrors.FreezeLimitReached);
        subscription.FrozenSince.ShouldBeNull();
    }

    [Fact]
    public void Unfreeze_NotFrozen_FailsWithNotFrozen() =>
        Sell().Unfreeze(Mid, MaxFreezeDays).Error.ShouldBe(SubscriptionErrors.NotFrozen);

    [Fact]
    public void Unfreeze_CancelledWhileFrozen_FailsWithCancelled()
    {
        var subscription = InState("frozen");
        subscription.Cancel("انصراف عضو", Mid, Now);

        subscription.Unfreeze(Mid.AddDays(3), MaxFreezeDays).Error.ShouldBe(SubscriptionErrors.Cancelled);
        subscription.EndDate.ShouldBe(End);
    }

    [Fact]
    public void Unfreeze_DateBeforeTheFreeze_Throws()
    {
        var subscription = Sell();
        subscription.Freeze(Mid, MaxFreezeDays);

        Should.Throw<ArgumentOutOfRangeException>(() => subscription.Unfreeze(Mid.AddDays(-1), MaxFreezeDays));
    }

    // ---- Cancel ----

    [Fact]
    public void Cancel_WithReason_RecordsTheMomentAndTheTrimmedReason()
    {
        var subscription = Sell();

        subscription.Cancel("  انصراف عضو  ", Mid, Now).IsSuccess.ShouldBeTrue();

        subscription.CancelledAt.ShouldBe(Now);
        subscription.CancellationReason.ShouldBe("انصراف عضو");
    }

    /// <summary>
    /// BUSINESS_RULES.md §4 Cancel: only a subscription nobody has used yet, and only while it is
    /// still something to decide about rather than history.
    /// </summary>
    [Theory]
    [InlineData("upcoming")]
    [InlineData("active")]
    [InlineData("frozen")]
    public void Cancel_UnusedAndNotFinished_Succeeds(string state) =>
        InState(state).Cancel("انصراف عضو", TodayFor(state), Now).IsSuccess.ShouldBeTrue();

    [Fact]
    public void Cancel_AfterASessionIsUsed_FailsWithAlreadyUsed()
    {
        var subscription = Sell();
        Succeed(subscription.ConsumeSession(Mid));

        subscription.Cancel("انصراف عضو", Mid, Now).Error.ShouldBe(SubscriptionErrors.AlreadyUsed);
        subscription.CancelledAt.ShouldBeNull();
    }

    /// <summary>
    /// The last session used means every session used, so "exhausted" is refused for having been
    /// used, not for having ended: the member's answer is the same either way.
    /// </summary>
    [Fact]
    public void Cancel_Exhausted_FailsWithAlreadyUsed() =>
        InState("exhausted").Cancel("انصراف عضو", Mid, Now).Error.ShouldBe(SubscriptionErrors.AlreadyUsed);

    [Fact]
    public void Cancel_Expired_FailsWithExpired()
    {
        var subscription = InState("expired");

        subscription.Cancel("انصراف عضو", TodayFor("expired"), Now).Error.ShouldBe(SubscriptionErrors.Expired);
        subscription.CancelledAt.ShouldBeNull();
    }

    /// <summary>
    /// The escape hatch of BUSINESS_RULES.md §4: cancel check-in restores the session, which brings
    /// used sessions back to zero and makes the subscription cancellable again.
    /// </summary>
    [Fact]
    public void Cancel_AfterTheUsedSessionIsRestored_Succeeds()
    {
        var subscription = Sell();
        Succeed(subscription.ConsumeSession(Mid));
        subscription.RestoreSession();

        subscription.Cancel("انصراف عضو", Mid, Now).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Cancel_Twice_FailsAndKeepsTheFirstReason()
    {
        var subscription = InState("cancelled");

        subscription.Cancel("دلیل دوم", Mid, Now.AddHours(1)).Error.ShouldBe(SubscriptionErrors.Cancelled);

        subscription.CancellationReason.ShouldBe("انصراف عضو");
        subscription.CancelledAt.ShouldBe(Now);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Cancel_BlankReason_FailsWithReasonRequired(string reason)
    {
        var subscription = Sell();

        subscription.Cancel(reason, Mid, Now).Error.ShouldBe(SubscriptionErrors.CancelReasonRequired);
        subscription.CancelledAt.ShouldBeNull();
    }

    [Fact]
    public void Cancel_ReasonOverTheLimit_FailsWithReasonTooLong()
    {
        var reason = new string('ا', Subscription.CancellationReasonMaxLength + 1);

        Sell().Cancel(reason, Mid, Now).Error.ShouldBe(SubscriptionErrors.CancelReasonTooLong);
    }

    [Fact]
    public void Cancel_ReasonAtTheLimit_Succeeds()
    {
        var reason = new string('ا', Subscription.CancellationReasonMaxLength);

        Sell().Cancel(reason, Mid, Now).IsSuccess.ShouldBeTrue();
    }

    // ---- ShiftQueued ----

    [Fact]
    public void ShiftQueued_MovesStartAndEndDateByTheSameNumberOfDays()
    {
        var subscription = Sell();

        subscription.ShiftQueued(3);

        subscription.StartDate.ShouldBe(Start.AddDays(3));
        subscription.EndDate.ShouldBe(End.AddDays(3));
    }

    [Fact]
    public void ShiftQueued_ZeroDays_LeavesDatesUnchanged()
    {
        var subscription = Sell();

        subscription.ShiftQueued(0);

        subscription.StartDate.ShouldBe(Start);
        subscription.EndDate.ShouldBe(End);
    }

    [Fact]
    public void ShiftQueued_NegativeDays_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Sell().ShiftQueued(-1));

    // ---- Helpers ----

    private static Subscription Sell(int sessions = 10) =>
        Subscription.CreateMembership(MemberId, sessions, SessionPrice, Start).Value;

    /// <summary>A subscription that has the given status on <see cref="TodayFor"/>.</summary>
    private static Subscription InState(string state)
    {
        var subscription = Sell(sessions: Subscription.MinSessionCount);

        switch (state)
        {
            case "upcoming" or "active" or "expired":
                break;
            case "exhausted":
                for (var visit = 0; visit < Subscription.MinSessionCount; visit++)
                {
                    Succeed(subscription.ConsumeSession(Mid));
                }

                break;
            case "frozen":
                Succeed(subscription.Freeze(Mid, MaxFreezeDays));
                break;
            case "cancelled":
                Succeed(subscription.Cancel("انصراف عضو", Mid, Now));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown state.");
        }

        subscription.GetStatus(TodayFor(state)).ToString().ShouldBe(state, StringCompareShould.IgnoreCase);

        return subscription;
    }

    private static DateOnly TodayFor(string state) => state switch
    {
        "upcoming" => Start.AddDays(-1),
        "expired" => End.AddDays(1),
        _ => Mid,
    };

    private static void Succeed(Result result) => result.IsSuccess.ShouldBeTrue();

    /// <summary>"Today" as the gym sees it: the date in Asia/Tehran (BUSINESS_RULES.md §0).</summary>
    private static DateOnly GymToday(TimeProvider time)
    {
        var tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), tehran).DateTime);
    }
}
