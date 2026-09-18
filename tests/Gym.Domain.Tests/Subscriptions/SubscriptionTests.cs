using Gym.Domain.Common;
using Gym.Domain.Plans;
using Gym.Domain.Subscriptions;

using Microsoft.Extensions.Time.Testing;

namespace Gym.Domain.Tests.Subscriptions;

/// <summary>
/// BUSINESS_RULES.md §4. Most tests sell a 30-day, 12-session plan starting on 2026-09-01, so
/// it ends on 2026-09-30.
/// </summary>
public sealed class SubscriptionTests
{
    private const int MaxFreezeDays = 30;

    private static readonly Guid MemberId = Guid.CreateVersion7();
    private static readonly DateOnly Start = new(2026, 9, 1);
    private static readonly DateOnly End = new(2026, 9, 30);
    private static readonly DateOnly Mid = new(2026, 9, 10);
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 8, 0, 0, TimeSpan.Zero);

    // ---- Create ----

    [Fact]
    public void Create_ActivePlan_CopiesTheSnapshot()
    {
        var plan = Plan.Create("یک ماهه ۱۲ جلسه", 30, 12, 900_000m).Value;

        var subscription = Subscription.Create(MemberId, plan, Start).Value;

        subscription.MemberId.ShouldBe(MemberId);
        subscription.PlanId.ShouldBe(plan.Id);
        subscription.PlanName.ShouldBe("یک ماهه ۱۲ جلسه");
        subscription.Price.ShouldBe(900_000m);
        subscription.DurationDays.ShouldBe(30);
        subscription.TotalSessions.ShouldBe(12);
        subscription.UsedSessions.ShouldBe(0);
        subscription.RemainingSessions.ShouldBe(12);
    }

    [Fact]
    public void Create_ThirtyDayPlan_EndDateIsInclusive()
    {
        var subscription = Sell(durationDays: 30);

        subscription.StartDate.ShouldBe(Start);
        subscription.EndDate.ShouldBe(new DateOnly(2026, 9, 30));
    }

    [Fact]
    public void Create_OneDayPlan_StartsAndEndsOnTheSameDay()
    {
        var subscription = Sell(durationDays: 1);

        subscription.EndDate.ShouldBe(Start);
        subscription.GetStatus(Start).ShouldBe(SubscriptionStatus.Active);
        subscription.GetStatus(Start.AddDays(1)).ShouldBe(SubscriptionStatus.Expired);
    }

    [Fact]
    public void Create_PlanEditedAfterSale_SubscriptionKeepsTheSnapshot()
    {
        var plan = Plan.Create("یک ماهه", 30, 12, 900_000m).Value;
        var subscription = Subscription.Create(MemberId, plan, Start).Value;

        plan.Update("یک ماهه جدید", 60, null, 1_200_000m);
        plan.Deactivate();

        subscription.PlanName.ShouldBe("یک ماهه");
        subscription.Price.ShouldBe(900_000m);
        subscription.DurationDays.ShouldBe(30);
        subscription.TotalSessions.ShouldBe(12);
        subscription.EndDate.ShouldBe(End);
    }

    [Fact]
    public void Create_InactivePlan_FailsWithPlanInactive()
    {
        var plan = Plan.Create("قدیمی", 30, 12, 900_000m).Value;
        plan.Deactivate();

        var result = Subscription.Create(MemberId, plan, Start);

        result.Error.ShouldBe(PlanErrors.Inactive);
    }

    [Fact]
    public void Create_UnlimitedPlan_HasNoSessionLimit()
    {
        var subscription = Sell(sessions: null);

        subscription.IsUnlimited.ShouldBeTrue();
        subscription.TotalSessions.ShouldBeNull();
        subscription.RemainingSessions.ShouldBeNull();
    }

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
        subscription.Cancel("انصراف عضو", Now).IsSuccess.ShouldBeTrue();

        subscription.GetStatus(Mid).ShouldBe(SubscriptionStatus.Cancelled);
    }

    [Fact]
    public void GetStatus_CancelledBeforeStart_IsCancelled()
    {
        var subscription = Sell();
        subscription.Cancel("اشتباه در ثبت", Now).IsSuccess.ShouldBeTrue();

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
        var subscription = Sell(sessions: 2);
        subscription.ConsumeSession(Mid).IsSuccess.ShouldBeTrue();

        subscription.ConsumeSession(Mid).IsSuccess.ShouldBeTrue();

        subscription.GetStatus(Mid).ShouldBe(SubscriptionStatus.Exhausted);
        subscription.ConsumeSession(Mid).Error.ShouldBe(SubscriptionErrors.NoSessionsLeft);
        subscription.UsedSessions.ShouldBe(2);
    }

    [Fact]
    public void ConsumeSession_UnlimitedPlan_IsNeverExhausted()
    {
        var subscription = Sell(sessions: null);

        for (var visit = 0; visit < 400; visit++)
        {
            subscription.ConsumeSession(Mid).IsSuccess.ShouldBeTrue();
        }

        subscription.UsedSessions.ShouldBe(400);
        subscription.GetStatus(Mid).ShouldBe(SubscriptionStatus.Active);
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
        var subscription = Sell(durationDays: 90);
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
        subscription.Cancel("انصراف عضو", Now);

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

        subscription.Cancel("  انصراف عضو  ", Now).IsSuccess.ShouldBeTrue();

        subscription.CancelledAt.ShouldBe(Now);
        subscription.CancellationReason.ShouldBe("انصراف عضو");
    }

    [Theory]
    [InlineData("upcoming")]
    [InlineData("active")]
    [InlineData("frozen")]
    [InlineData("exhausted")]
    [InlineData("expired")]
    public void Cancel_AnyStatusButCancelled_Succeeds(string state) =>
        InState(state).Cancel("انصراف عضو", Now).IsSuccess.ShouldBeTrue();

    [Fact]
    public void Cancel_Twice_FailsAndKeepsTheFirstReason()
    {
        var subscription = InState("cancelled");

        subscription.Cancel("دلیل دوم", Now.AddHours(1)).Error.ShouldBe(SubscriptionErrors.Cancelled);

        subscription.CancellationReason.ShouldBe("انصراف عضو");
        subscription.CancelledAt.ShouldBe(Now);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Cancel_BlankReason_FailsWithReasonRequired(string reason)
    {
        var subscription = Sell();

        subscription.Cancel(reason, Now).Error.ShouldBe(SubscriptionErrors.CancelReasonRequired);
        subscription.CancelledAt.ShouldBeNull();
    }

    [Fact]
    public void Cancel_ReasonOverTheLimit_FailsWithReasonTooLong()
    {
        var reason = new string('ا', Subscription.CancellationReasonMaxLength + 1);

        Sell().Cancel(reason, Now).Error.ShouldBe(SubscriptionErrors.CancelReasonTooLong);
    }

    [Fact]
    public void Cancel_ReasonAtTheLimit_Succeeds()
    {
        var reason = new string('ا', Subscription.CancellationReasonMaxLength);

        Sell().Cancel(reason, Now).IsSuccess.ShouldBeTrue();
    }

    // ---- Helpers ----

    private static Subscription Sell(int durationDays = 30, int? sessions = 12)
    {
        var plan = Plan.Create("پلن", durationDays, sessions, 900_000m).Value;

        return Subscription.Create(MemberId, plan, Start).Value;
    }

    /// <summary>A subscription that has the given status on <see cref="TodayFor"/>.</summary>
    private static Subscription InState(string state)
    {
        var subscription = Sell(sessions: 2);

        switch (state)
        {
            case "upcoming" or "active" or "expired":
                break;
            case "exhausted":
                Succeed(subscription.ConsumeSession(Mid));
                Succeed(subscription.ConsumeSession(Mid));
                break;
            case "frozen":
                Succeed(subscription.Freeze(Mid, MaxFreezeDays));
                break;
            case "cancelled":
                Succeed(subscription.Cancel("انصراف عضو", Now));
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
