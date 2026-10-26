using Gym.Domain.Notifications;

namespace Gym.Domain.Tests.Notifications;

/// <summary>BUSINESS_RULES.md §12 <i>The absence SMS's effect</i>: what became of one absence SMS.</summary>
public sealed class AbsenceSmsEffectTests
{
    private static readonly DateOnly SentOn = new(2026, 10, 10);

    /// <summary>A plan that covers the whole window and more.</summary>
    private static readonly DateOnly PlanEnd = SentOn.AddDays(30);

    private static readonly DateOnly Later = SentOn.AddDays(30);

    private static AbsenceSmsFollowUp FollowUp(
        DateOnly? firstVisitOn, DateOnly? today = null, DateOnly? planEndOn = null, DateOnly? frozenSince = null) =>
        AbsenceSmsEffect.FollowUp(SentOn, planEndOn ?? PlanEnd, frozenSince, firstVisitOn, today ?? Later);

    // ---- Which messages count ----

    [Fact]
    public void Counts_SentWithNoDeliveryYet_Counts() =>
        AbsenceSmsEffect.Counts(NotificationStatus.Sent, null).ShouldBeTrue();

    [Theory]
    [InlineData(SmsDelivery.Delivered)]
    [InlineData(SmsDelivery.NotDelivered)]
    public void Counts_SentAndNotGivenBack_Counts(SmsDelivery delivery) =>
        AbsenceSmsEffect.Counts(NotificationStatus.Sent, delivery).ShouldBeTrue();

    [Theory]
    [InlineData(SmsDelivery.Cancelled)]
    [InlineData(SmsDelivery.BlockedByReceiver)]
    public void Counts_GivenBackByTheProvider_DoesNotCount(SmsDelivery delivery) =>
        AbsenceSmsEffect.Counts(NotificationStatus.Sent, delivery).ShouldBeFalse();

    [Theory]
    [InlineData(NotificationStatus.Pending)]
    [InlineData(NotificationStatus.Failed)]
    [InlineData(NotificationStatus.Unknown)]
    public void Counts_NotSent_DoesNotCount(NotificationStatus status) =>
        AbsenceSmsEffect.Counts(status, null).ShouldBeFalse();

    // ---- Came back, waiting, did not ----

    [Fact]
    public void FollowUp_CameTheSameDay_CameBackAfterZeroDays() =>
        FollowUp(SentOn).ShouldBe(new AbsenceSmsFollowUp(AbsenceSmsOutcome.CameBack, 0, null));

    [Fact]
    public void FollowUp_CameOnTheWindowsLastDay_CameBack() =>
        FollowUp(SentOn.AddDays(5)).ShouldBe(new AbsenceSmsFollowUp(AbsenceSmsOutcome.CameBack, 5, null));

    [Fact]
    public void FollowUp_CameTheDayAfterTheWindow_DidNotComeBack() =>
        FollowUp(SentOn.AddDays(6)).ShouldBe(new AbsenceSmsFollowUp(AbsenceSmsOutcome.DidNotComeBack, null, null));

    [Fact]
    public void FollowUp_NotComeYetInsideTheWindow_IsWaitingWithItsDaysLeft() =>
        FollowUp(null, today: SentOn.AddDays(2)).ShouldBe(new AbsenceSmsFollowUp(AbsenceSmsOutcome.Waiting, null, 3));

    [Fact]
    public void FollowUp_NotComeOnTheWindowsLastDay_IsStillWaiting() =>
        FollowUp(null, today: SentOn.AddDays(5)).ShouldBe(new AbsenceSmsFollowUp(AbsenceSmsOutcome.Waiting, null, 0));

    [Fact]
    public void FollowUp_NotComeAndTheWindowOver_DidNotComeBack() =>
        FollowUp(null, today: SentOn.AddDays(6)).ShouldBe(new AbsenceSmsFollowUp(AbsenceSmsOutcome.DidNotComeBack, null, null));

    // ---- Not measurable ----

    [Fact]
    public void FollowUp_PlanEndsOnTheWindowsLastDay_IsMeasured() =>
        FollowUp(null, planEndOn: SentOn.AddDays(5)).Outcome.ShouldBe(AbsenceSmsOutcome.DidNotComeBack);

    [Fact]
    public void FollowUp_PlanEndsInsideTheWindow_IsNotMeasurableEvenIfTheMemberCame() =>
        FollowUp(SentOn.AddDays(1), planEndOn: SentOn.AddDays(4)).Outcome.ShouldBe(AbsenceSmsOutcome.NotMeasurable);

    [Fact]
    public void FollowUp_NoPlanFoundAnyMore_IsNotMeasurable() =>
        AbsenceSmsEffect.FollowUp(SentOn, null, null, SentOn.AddDays(1), Later).Outcome.ShouldBe(AbsenceSmsOutcome.NotMeasurable);

    [Fact]
    public void FollowUp_FrozenInsideTheWindowBeforeComing_IsNotMeasurable() =>
        FollowUp(null, frozenSince: SentOn.AddDays(2)).Outcome.ShouldBe(AbsenceSmsOutcome.NotMeasurable);

    [Fact]
    public void FollowUp_FrozenAfterComingBack_CameBack() =>
        FollowUp(SentOn.AddDays(1), frozenSince: SentOn.AddDays(3)).Outcome.ShouldBe(AbsenceSmsOutcome.CameBack);

    [Fact]
    public void FollowUp_FrozenAfterTheWindow_DidNotComeBack() =>
        FollowUp(null, frozenSince: SentOn.AddDays(6)).Outcome.ShouldBe(AbsenceSmsOutcome.DidNotComeBack);

    [Fact]
    public void FollowUp_AVisitBeforeTheMessage_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => FollowUp(SentOn.AddDays(-1)));
}
