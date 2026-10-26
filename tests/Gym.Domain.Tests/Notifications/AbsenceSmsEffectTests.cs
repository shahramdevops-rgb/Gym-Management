using Gym.Domain.Notifications;

namespace Gym.Domain.Tests.Notifications;

/// <summary>BUSINESS_RULES.md §12 <i>The absence SMS's effect</i>: what became of one absence SMS.</summary>
public sealed class AbsenceSmsEffectTests
{
    private static readonly DateOnly SentOn = new(2026, 10, 10);

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

    // ---- What became of it ----

    [Fact]
    public void FollowUp_CameTheSameDay_CameBackAfterZeroDays() =>
        AbsenceSmsEffect.FollowUp(SentOn, SentOn, SentOn.AddDays(30))
            .ShouldBe(new AbsenceSmsFollowUp(AbsenceSmsOutcome.CameBack, 0, null));

    [Fact]
    public void FollowUp_CameOnTheWindowsLastDay_CameBack() =>
        AbsenceSmsEffect.FollowUp(SentOn, SentOn.AddDays(14), SentOn.AddDays(30))
            .ShouldBe(new AbsenceSmsFollowUp(AbsenceSmsOutcome.CameBack, 14, null));

    [Fact]
    public void FollowUp_CameTheDayAfterTheWindow_DidNotComeBack() =>
        AbsenceSmsEffect.FollowUp(SentOn, SentOn.AddDays(15), SentOn.AddDays(30))
            .ShouldBe(new AbsenceSmsFollowUp(AbsenceSmsOutcome.DidNotComeBack, null, null));

    [Fact]
    public void FollowUp_NotComeYetInsideTheWindow_IsWaitingWithItsDaysLeft() =>
        AbsenceSmsEffect.FollowUp(SentOn, null, SentOn.AddDays(4))
            .ShouldBe(new AbsenceSmsFollowUp(AbsenceSmsOutcome.Waiting, null, 10));

    [Fact]
    public void FollowUp_NotComeOnTheWindowsLastDay_IsStillWaiting() =>
        AbsenceSmsEffect.FollowUp(SentOn, null, SentOn.AddDays(14))
            .ShouldBe(new AbsenceSmsFollowUp(AbsenceSmsOutcome.Waiting, null, 0));

    [Fact]
    public void FollowUp_NotComeAndTheWindowOver_DidNotComeBack() =>
        AbsenceSmsEffect.FollowUp(SentOn, null, SentOn.AddDays(15))
            .ShouldBe(new AbsenceSmsFollowUp(AbsenceSmsOutcome.DidNotComeBack, null, null));

    [Fact]
    public void FollowUp_AVisitBeforeTheMessage_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => AbsenceSmsEffect.FollowUp(SentOn, SentOn.AddDays(-1), SentOn));
}
