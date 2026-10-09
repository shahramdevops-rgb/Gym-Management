using Gym.Domain.Notifications;

namespace Gym.Domain.Tests.Notifications;

/// <summary>BUSINESS_RULES.md §10, as far as one message can answer it.</summary>
public sealed class NotificationTests
{
    private const string Phone = "+989121234567";
    private const string Text = "سارا محمدی عزیز، اشتراک شما در باشگاه پاسارگاد ۱۴۰۵/۰۷/۲۰ به پایان می‌رسد.";
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid SubscriptionId = Guid.NewGuid();
    private static readonly Guid PayableId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 7, 0, 0, TimeSpan.Zero);

    // ---- Creating ----

    [Fact]
    public void ForSubscriptionExpiring_Valid_IsPendingWithItsSubscription()
    {
        var notification = Notification.ForSubscriptionExpiring(MemberId, SubscriptionId, Phone, Text);

        notification.Kind.ShouldBe(NotificationKind.SubscriptionExpiring);
        notification.Recipient.ShouldBe(Phone);
        notification.MemberId.ShouldBe(MemberId);
        notification.SubscriptionId.ShouldBe(SubscriptionId);
        notification.PayableId.ShouldBeNull();
        notification.JalaliYear.ShouldBeNull();
        notification.Text.ShouldBe(Text);
        notification.Status.ShouldBe(NotificationStatus.Pending);
        notification.Attempts.ShouldBe(0);
        notification.LastAttemptAt.ShouldBeNull();
        notification.ProviderMessageId.ShouldBeNull();
        notification.CostRial.ShouldBeNull();
        notification.SentAt.ShouldBeNull();
        notification.Delivery.ShouldBeNull();
    }

    [Fact]
    public void ForLowSessions_Valid_IsPendingWithItsSubscription()
    {
        var notification = Notification.ForLowSessions(MemberId, SubscriptionId, Phone, Text);

        notification.Kind.ShouldBe(NotificationKind.LowSessions);
        notification.MemberId.ShouldBe(MemberId);
        notification.SubscriptionId.ShouldBe(SubscriptionId);
        notification.Status.ShouldBe(NotificationStatus.Pending);
    }

    [Fact]
    public void ForBirthday_Valid_KeepsTheJalaliYearAndNoSubscription()
    {
        var notification = Notification.ForBirthday(MemberId, 1405, Phone, Text);

        notification.Kind.ShouldBe(NotificationKind.Birthday);
        notification.MemberId.ShouldBe(MemberId);
        notification.JalaliYear.ShouldBe(1405);
        notification.SubscriptionId.ShouldBeNull();
        notification.PayableId.ShouldBeNull();
    }

    [Fact]
    public void ForPayableDue_Valid_NamesThePayableAndNoMember()
    {
        var notification = Notification.ForPayableDue(PayableId, Phone, Text);

        notification.Kind.ShouldBe(NotificationKind.PayableDue);
        notification.PayableId.ShouldBe(PayableId);
        notification.MemberId.ShouldBeNull();
        notification.SubscriptionId.ShouldBeNull();
        notification.JalaliYear.ShouldBeNull();
    }

    [Fact]
    public void Create_TextAtTheLimit_IsKept()
    {
        var text = new string('x', Notification.TextMaxLength);

        Notification.ForPayableDue(PayableId, Phone, text).Text.ShouldBe(text);
    }

    [Fact]
    public void Create_TextOverTheLimit_Throws()
    {
        Should.Throw<ArgumentException>(
            () => Notification.ForPayableDue(PayableId, Phone, new string('x', Notification.TextMaxLength + 1)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankRecipient_Throws(string recipient)
    {
        Should.Throw<ArgumentException>(
            () => Notification.ForSubscriptionExpiring(MemberId, SubscriptionId, recipient, Text));
    }

    [Fact]
    public void Create_BlankText_Throws()
    {
        Should.Throw<ArgumentException>(
            () => Notification.ForSubscriptionExpiring(MemberId, SubscriptionId, Phone, " "));
    }

    [Fact]
    public void Create_EmptyEventId_Throws()
    {
        Should.Throw<ArgumentException>(
            () => Notification.ForSubscriptionExpiring(MemberId, Guid.Empty, Phone, Text));
        Should.Throw<ArgumentException>(
            () => Notification.ForPayableDue(Guid.Empty, Phone, Text));
    }

    [Fact]
    public void ForBirthday_YearNotPositive_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => Notification.ForBirthday(MemberId, 0, Phone, Text));
    }

    // ---- Sending ----

    [Fact]
    public void MarkSent_Pending_KeepsTheProvidersIdCostAndMoment()
    {
        var notification = Pending();

        notification.MarkSent(8_792_343, 1_200m, Now).IsSuccess.ShouldBeTrue();

        notification.Status.ShouldBe(NotificationStatus.Sent);
        notification.ProviderMessageId.ShouldBe(8_792_343);
        notification.CostRial.ShouldBe(1_200m);
        notification.SentAt.ShouldBe(Now);
        notification.Attempts.ShouldBe(1);
        notification.LastAttemptAt.ShouldBe(Now);
    }

    [Fact]
    public void MarkSent_AfterARetryableFailure_ClearsTheErrorCode()
    {
        var notification = Pending();
        notification.RecordRetryableFailure(409, Now);

        notification.MarkSent(1, 1_200m, Now.AddMinutes(5));

        notification.ErrorCode.ShouldBeNull();
        notification.Attempts.ShouldBe(2);
        notification.LastAttemptAt.ShouldBe(Now.AddMinutes(5));
    }

    [Fact]
    public void RecordRetryableFailure_Pending_StaysPendingAndCountsTheAttempt()
    {
        var notification = Pending();

        notification.RecordRetryableFailure(409, Now).IsSuccess.ShouldBeTrue();

        notification.Status.ShouldBe(NotificationStatus.Pending);
        notification.ErrorCode.ShouldBe(409);
        notification.Attempts.ShouldBe(1);
    }

    [Fact]
    public void MarkFailed_Pending_IsFailedWithTheCode()
    {
        var notification = Pending();

        notification.MarkFailed(424, Now).IsSuccess.ShouldBeTrue();

        notification.Status.ShouldBe(NotificationStatus.Failed);
        notification.ErrorCode.ShouldBe(424);
        notification.Attempts.ShouldBe(1);
        notification.SentAt.ShouldBeNull();
    }

    [Fact]
    public void MarkUnknown_Pending_IsUnknownAndCountsTheAttempt()
    {
        var notification = Pending();

        notification.MarkUnknown(Now).IsSuccess.ShouldBeTrue();

        notification.Status.ShouldBe(NotificationStatus.Unknown);
        notification.Attempts.ShouldBe(1);
        notification.SentAt.ShouldBeNull();
    }

    [Fact]
    public void GiveUp_AfterRetryableFailures_IsFailedWithoutAnotherAttempt()
    {
        var notification = Pending();
        notification.RecordRetryableFailure(409, Now);
        notification.RecordRetryableFailure(409, Now.AddMinutes(1));

        notification.GiveUp().IsSuccess.ShouldBeTrue();

        notification.Status.ShouldBe(NotificationStatus.Failed);
        notification.Attempts.ShouldBe(2);
        notification.ErrorCode.ShouldBe(409);
    }

    [Fact]
    public void MarkInterrupted_LeftPendingByAStoppedRun_IsUnknownWithNoAttemptCounted()
    {
        // The request may or may not have left: never sent again by itself (§10 The daily runs).
        var notification = Pending();

        notification.MarkInterrupted().IsSuccess.ShouldBeTrue();

        notification.Status.ShouldBe(NotificationStatus.Unknown);
        notification.Attempts.ShouldBe(0);
        notification.LastAttemptAt.ShouldBeNull();
    }

    [Fact]
    public void MarkInterrupted_AfterARetryableFailure_KeepsItsAttemptsAndCode()
    {
        var notification = Pending();
        notification.RecordRetryableFailure(409, Now);

        notification.MarkInterrupted().IsSuccess.ShouldBeTrue();

        notification.Status.ShouldBe(NotificationStatus.Unknown);
        notification.Attempts.ShouldBe(1);
        notification.ErrorCode.ShouldBe(409);
    }

    public static TheoryData<string> Settled => ["Sent", "Failed", "Unknown"];

    [Theory]
    [MemberData(nameof(Settled))]
    public void EveryOutcome_NotPending_IsRefusedAndChangesNothing(string status)
    {
        var notification = Settle(status);
        var attempts = notification.Attempts;

        notification.MarkSent(2, 1_200m, Now).Error.ShouldBe(NotificationErrors.NotPending);
        notification.RecordRetryableFailure(409, Now).Error.ShouldBe(NotificationErrors.NotPending);
        notification.MarkFailed(424, Now).Error.ShouldBe(NotificationErrors.NotPending);
        notification.MarkUnknown(Now).Error.ShouldBe(NotificationErrors.NotPending);
        notification.GiveUp().Error.ShouldBe(NotificationErrors.NotPending);
        notification.MarkInterrupted().Error.ShouldBe(NotificationErrors.NotPending);

        notification.Status.ToString().ShouldBe(status);
        notification.Attempts.ShouldBe(attempts);
    }

    [Fact]
    public void MarkSent_NegativeCost_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Pending().MarkSent(1, -1m, Now));
    }

    // ---- Resend (task 10.5) ----

    [Theory]
    [InlineData("Failed")]
    [InlineData("Unknown")]
    public void PrepareResend_FailedOrUnknown_IsPendingAgainWithItsAttemptsKept(string status)
    {
        var notification = Settle(status);

        notification.PrepareResend().IsSuccess.ShouldBeTrue();

        notification.Status.ShouldBe(NotificationStatus.Pending);
        notification.Attempts.ShouldBe(1);
        notification.LastAttemptAt.ShouldBe(Now);
    }

    [Fact]
    public void PrepareResend_Always_KeepsExactlyTheSameMessage()
    {
        var notification = Settle("Failed");

        notification.PrepareResend();

        notification.Recipient.ShouldBe(Phone);
        notification.Text.ShouldBe(Text);
    }

    [Fact]
    public void PrepareResend_Failed_ClearsTheOldCode()
    {
        var notification = Settle("Failed");

        notification.PrepareResend();

        notification.ErrorCode.ShouldBeNull();
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Sent")]
    public void PrepareResend_PendingOrSent_IsRefusedAndChangesNothing(string status)
    {
        var notification = Settle(status);

        notification.PrepareResend().Error.ShouldBe(NotificationErrors.NotResendable);

        notification.Status.ToString().ShouldBe(status);
    }

    [Fact]
    public void PrepareResend_ThenSent_IsSentWithBothAttemptsCounted()
    {
        var notification = Settle("Unknown");
        var later = Now.AddDays(1);

        notification.PrepareResend();
        notification.MarkSent(7, 1_350m, later).IsSuccess.ShouldBeTrue();

        notification.Status.ShouldBe(NotificationStatus.Sent);
        notification.Attempts.ShouldBe(2);
        notification.SentAt.ShouldBe(later);
        notification.CostRial.ShouldBe(1_350m);
    }

    [Fact]
    public void PrepareResend_StoppedBeforeItsOutcome_BecomesUnknownAtTheNextRun()
    {
        var notification = Settle("Failed");
        notification.PrepareResend();

        notification.MarkInterrupted().IsSuccess.ShouldBeTrue();

        notification.Status.ShouldBe(NotificationStatus.Unknown);
        notification.ErrorCode.ShouldBeNull();
    }

    // ---- Delivery ----

    [Theory]
    [InlineData(SmsDelivery.Delivered)]
    [InlineData(SmsDelivery.NotDelivered)]
    [InlineData(SmsDelivery.BlockedByReceiver)]
    public void RecordDelivery_Sent_KeepsIt(SmsDelivery delivery)
    {
        var notification = Settle("Sent");

        notification.RecordDelivery(delivery).IsSuccess.ShouldBeTrue();

        notification.Delivery.ShouldBe(delivery);
    }

    [Fact]
    public void RecordDelivery_Twice_KeepsTheLatest()
    {
        // The provider can say "not delivered" first and "delivered" once the phone is back on.
        var notification = Settle("Sent");
        notification.RecordDelivery(SmsDelivery.NotDelivered);

        notification.RecordDelivery(SmsDelivery.Delivered);

        notification.Delivery.ShouldBe(SmsDelivery.Delivered);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Failed")]
    [InlineData("Unknown")]
    public void RecordDelivery_NotSent_IsRefused(string status)
    {
        var notification = Settle(status);

        notification.RecordDelivery(SmsDelivery.Delivered).Error.ShouldBe(NotificationErrors.NotSent);

        notification.Delivery.ShouldBeNull();
    }

    // ---- Helpers ----

    private static Notification Pending() =>
        Notification.ForSubscriptionExpiring(MemberId, SubscriptionId, Phone, Text);

    private static Notification Settle(string status)
    {
        var notification = Pending();
        switch (status)
        {
            case "Sent":
                notification.MarkSent(1, 1_200m, Now);
                break;
            case "Failed":
                notification.MarkFailed(424, Now);
                break;
            case "Unknown":
                notification.MarkUnknown(Now);
                break;
        }

        return notification;
    }
}
