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
    public void ForDailyReport_Valid_NamesTheDayAndNothingElse()
    {
        var day = new DateOnly(2026, 10, 19);

        var notification = Notification.ForDailyReport(day, Phone, Text);

        notification.Kind.ShouldBe(NotificationKind.DailyReport);
        notification.ReportDate.ShouldBe(day);
        notification.Recipient.ShouldBe(Phone);
        notification.Status.ShouldBe(NotificationStatus.Pending);
        notification.MemberId.ShouldBeNull();
        notification.SubscriptionId.ShouldBeNull();
        notification.PayableId.ShouldBeNull();
        notification.JalaliYear.ShouldBeNull();
    }

    [Fact]
    public void ForSubscriptionReceipt_NamesTheMemberAndTheSubscription()
    {
        var memberId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();

        var notification = Notification.ForSubscriptionReceipt(memberId, subscriptionId, Phone, Text);

        notification.Kind.ShouldBe(NotificationKind.SubscriptionReceipt);
        notification.MemberId.ShouldBe(memberId);
        notification.SubscriptionId.ShouldBe(subscriptionId);
        notification.Recipient.ShouldBe(Phone);
        notification.Status.ShouldBe(NotificationStatus.Pending);
        notification.PayableId.ShouldBeNull();
        notification.JalaliYear.ShouldBeNull();
        notification.ReportDate.ShouldBeNull();
    }

    [Fact]
    public void ForWelcome_NamesTheMemberAndTheirFirstPlan()
    {
        var memberId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();

        var notification = Notification.ForWelcome(memberId, subscriptionId, Phone, Text);

        notification.Kind.ShouldBe(NotificationKind.Welcome);
        notification.MemberId.ShouldBe(memberId);
        notification.SubscriptionId.ShouldBe(subscriptionId);
        notification.Status.ShouldBe(NotificationStatus.Pending);
        notification.PayableId.ShouldBeNull();
        notification.JalaliYear.ShouldBeNull();
        notification.ReportDate.ShouldBeNull();
    }

    [Fact]
    public void ForDebtReminder_NamesTheMemberAndTheDayTheDebtBegan()
    {
        var memberId = Guid.NewGuid();
        var since = new DateOnly(2026, 9, 1);

        var notification = Notification.ForDebtReminder(memberId, since, Phone, Text);

        notification.Kind.ShouldBe(NotificationKind.DebtReminder);
        notification.MemberId.ShouldBe(memberId);
        notification.DebtSince.ShouldBe(since);
        notification.Recipient.ShouldBe(Phone);
        notification.Status.ShouldBe(NotificationStatus.Pending);
        notification.SubscriptionId.ShouldBeNull();
        notification.PayableId.ShouldBeNull();
        notification.JalaliYear.ShouldBeNull();
        notification.ReportDate.ShouldBeNull();
    }

    [Fact]
    public void ForDebtReminder_EmptyMemberId_Throws() =>
        Should.Throw<ArgumentException>(() => Notification.ForDebtReminder(Guid.Empty, new DateOnly(2026, 9, 1), Phone, Text));

    [Fact]
    public void ForAbsence_NamesTheMemberAndTheDayTheAbsenceCountsFrom()
    {
        var memberId = Guid.NewGuid();
        var since = new DateOnly(2026, 10, 1);

        var notification = Notification.ForAbsence(memberId, since, Phone, Text);

        notification.Kind.ShouldBe(NotificationKind.Absence);
        notification.MemberId.ShouldBe(memberId);
        notification.AbsentSince.ShouldBe(since);
        notification.Recipient.ShouldBe(Phone);
        notification.Status.ShouldBe(NotificationStatus.Pending);
        notification.SubscriptionId.ShouldBeNull();
        notification.PayableId.ShouldBeNull();
        notification.JalaliYear.ShouldBeNull();
        notification.ReportDate.ShouldBeNull();
        notification.DebtSince.ShouldBeNull();
    }

    [Fact]
    public void ForWelcomeBack_NamesTheMemberAndTheAbsenceTheyCameBackFrom()
    {
        var memberId = Guid.NewGuid();
        var since = new DateOnly(2026, 10, 1);

        var notification = Notification.ForWelcomeBack(memberId, since, Phone, Text);

        notification.Kind.ShouldBe(NotificationKind.WelcomeBack);
        notification.MemberId.ShouldBe(memberId);
        notification.AbsentSince.ShouldBe(since);
        notification.Recipient.ShouldBe(Phone);
        notification.Status.ShouldBe(NotificationStatus.Pending);
        notification.SubscriptionId.ShouldBeNull();
        notification.DebtSince.ShouldBeNull();
        notification.BroadcastId.ShouldBeNull();
    }

    [Fact]
    public void ForTrainerRequest_GoesToTheTrainerAboutTheMemberOnTheDayOfTheRequest()
    {
        var memberId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        var day = new DateOnly(2026, 10, 29);

        var notification = Notification.ForTrainerRequest(memberId, trainerId, day, Phone, Text);

        notification.Kind.ShouldBe(NotificationKind.TrainerRequest);
        notification.MemberId.ShouldBe(memberId);
        notification.TrainerId.ShouldBe(trainerId);
        notification.ReportDate.ShouldBe(day);
        notification.Recipient.ShouldBe(Phone);
        notification.Status.ShouldBe(NotificationStatus.Pending);
        notification.SubscriptionId.ShouldBeNull();
        notification.AbsentSince.ShouldBeNull();
    }

    [Fact]
    public void ForTrainerRequest_EmptyTrainerId_Throws() =>
        Should.Throw<ArgumentException>(() => Notification.ForTrainerRequest(Guid.NewGuid(), Guid.Empty, new DateOnly(2026, 10, 29), Phone, Text));

    [Fact]
    public void ForAbsence_EmptyMemberId_Throws() =>
        Should.Throw<ArgumentException>(() => Notification.ForAbsence(Guid.Empty, new DateOnly(2026, 10, 1), Phone, Text));

    [Fact]
    public void ForBroadcast_NamesTheBroadcastAndTheMember()
    {
        var broadcastId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var notification = Notification.ForBroadcast(broadcastId, memberId, Phone, Text);

        notification.Kind.ShouldBe(NotificationKind.Broadcast);
        notification.BroadcastId.ShouldBe(broadcastId);
        notification.MemberId.ShouldBe(memberId);
        notification.Status.ShouldBe(NotificationStatus.Pending);
        notification.SubscriptionId.ShouldBeNull();
        notification.PayableId.ShouldBeNull();
        notification.JalaliYear.ShouldBeNull();
        notification.ReportDate.ShouldBeNull();
        notification.DebtSince.ShouldBeNull();
        notification.AbsentSince.ShouldBeNull();
    }

    [Fact]
    public void ForBroadcast_EmptyBroadcastId_Throws() =>
        Should.Throw<ArgumentException>(() => Notification.ForBroadcast(Guid.Empty, Guid.NewGuid(), Phone, Text));

    [Fact]
    public void ForBroadcast_EmptyMemberId_Throws() =>
        Should.Throw<ArgumentException>(() => Notification.ForBroadcast(Guid.NewGuid(), Guid.Empty, Phone, Text));

    [Fact]
    public void ForSubscriptionReceipt_EmptySubscriptionId_Throws()
    {
        Should.Throw<ArgumentException>(() => Notification.ForSubscriptionReceipt(Guid.NewGuid(), Guid.Empty, Phone, Text));
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

    // ---- Resend after the provider cancelled it (task 10.16) ----

    [Fact]
    public void PrepareResend_CancelledByTheProvider_IsPendingAgainWithNothingLeftOfTheLastRequest()
    {
        // §10: it never reached the phone and its cost was given back, so the Owner may send it again.
        var notification = Cancelled();

        notification.PrepareResend().IsSuccess.ShouldBeTrue();

        notification.Status.ShouldBe(NotificationStatus.Pending);
        notification.ProviderMessageId.ShouldBeNull();
        notification.CostRial.ShouldBeNull();
        notification.SentAt.ShouldBeNull();
        notification.Delivery.ShouldBeNull();
        notification.Attempts.ShouldBe(1);
        notification.AutoResends.ShouldBe(0);
        notification.Text.ShouldBe(Text);
        notification.Recipient.ShouldBe(Phone);
    }

    [Theory]
    [InlineData(SmsDelivery.Delivered)]
    [InlineData(SmsDelivery.NotDelivered)]
    [InlineData(SmsDelivery.BlockedByReceiver)]
    public void PrepareResend_SentAndNotCancelled_IsRefusedAndChangesNothing(SmsDelivery delivery)
    {
        // Delivered or not delivered was paid for; a blocked one would be blocked again.
        var notification = Settle("Sent");
        notification.RecordDelivery(delivery);

        notification.PrepareResend().Error.ShouldBe(NotificationErrors.NotResendable);

        notification.Status.ShouldBe(NotificationStatus.Sent);
        notification.Delivery.ShouldBe(delivery);
    }

    [Fact]
    public void PrepareAutoResend_Cancelled_IsPendingAgainAndCounted()
    {
        var notification = Cancelled();

        notification.PrepareAutoResend().IsSuccess.ShouldBeTrue();

        notification.Status.ShouldBe(NotificationStatus.Pending);
        notification.Delivery.ShouldBeNull();
        notification.ProviderMessageId.ShouldBeNull();
        notification.AutoResends.ShouldBe(1);
    }

    [Fact]
    public void PrepareAutoResend_FifthCancelled_IsTheLastOne()
    {
        var notification = Cancelled();
        for (var resend = 1; resend < Notification.MaxAutoResends; resend++)
        {
            CancelAgain(notification);
        }

        notification.AutoResends.ShouldBe(Notification.MaxAutoResends - 1);
        notification.PrepareAutoResend().IsSuccess.ShouldBeTrue();
        notification.AutoResends.ShouldBe(5);

        notification.MarkSent(99, 1_200m, Now).IsSuccess.ShouldBeTrue();
        notification.RecordDelivery(SmsDelivery.Cancelled);
        notification.PrepareAutoResend().Error.ShouldBe(NotificationErrors.AutoResendsUsedUp);
        notification.Status.ShouldBe(NotificationStatus.Sent);
        notification.AutoResends.ShouldBe(5);
    }

    [Fact]
    public void PrepareResend_AfterTheAutomaticResendsAreUsedUp_IsStillAllowed()
    {
        // §10: after five, only the Owner's resend is left.
        var notification = Cancelled();
        for (var resend = 0; resend < Notification.MaxAutoResends; resend++)
        {
            CancelAgain(notification);
        }

        notification.PrepareResend().IsSuccess.ShouldBeTrue();

        notification.AutoResends.ShouldBe(Notification.MaxAutoResends);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Failed")]
    [InlineData("Unknown")]
    [InlineData("Sent")]
    public void PrepareAutoResend_NotCancelled_IsRefusedAndChangesNothing(string status)
    {
        // A failure or an unknown outcome is the Owner's to resend: it may cost money or arrive twice.
        var notification = Settle(status);

        notification.PrepareAutoResend().Error.ShouldBe(NotificationErrors.NotCancelledByProvider);

        notification.Status.ToString().ShouldBe(status);
        notification.AutoResends.ShouldBe(0);
    }

    [Theory]
    [InlineData(SmsDelivery.Delivered)]
    [InlineData(SmsDelivery.NotDelivered)]
    [InlineData(SmsDelivery.BlockedByReceiver)]
    public void PrepareAutoResend_SentAndNotCancelled_IsRefused(SmsDelivery delivery)
    {
        var notification = Settle("Sent");
        notification.RecordDelivery(delivery);

        notification.PrepareAutoResend().Error.ShouldBe(NotificationErrors.NotCancelledByProvider);
    }

    [Fact]
    public void PrepareAutoResend_ThenSent_KeepsTheNewRequestAndCountsEveryAttempt()
    {
        var notification = Cancelled();
        var later = Now.AddMinutes(30);

        notification.PrepareAutoResend();
        notification.MarkSent(8, 1_350m, later).IsSuccess.ShouldBeTrue();

        notification.ProviderMessageId.ShouldBe(8);
        notification.CostRial.ShouldBe(1_350m);
        notification.SentAt.ShouldBe(later);
        notification.Delivery.ShouldBeNull();
        notification.Attempts.ShouldBe(2);
    }

    [Fact]
    public void IsCancelledByProvider_OnlyASentMessageTheProviderCancelled()
    {
        Cancelled().IsCancelledByProvider.ShouldBeTrue();
        Settle("Sent").IsCancelledByProvider.ShouldBeFalse();
        Settle("Failed").IsCancelledByProvider.ShouldBeFalse();
    }

    // ---- Delivery ----

    [Theory]
    [InlineData(SmsDelivery.Delivered)]
    [InlineData(SmsDelivery.NotDelivered)]
    [InlineData(SmsDelivery.BlockedByReceiver)]
    [InlineData(SmsDelivery.Cancelled)]
    public void RecordDelivery_Sent_KeepsIt(SmsDelivery delivery)
    {
        var notification = Settle("Sent");

        notification.RecordDelivery(delivery).IsSuccess.ShouldBeTrue();

        notification.Delivery.ShouldBe(delivery);
    }

    [Theory]
    [InlineData(SmsDelivery.Delivered)]
    [InlineData(SmsDelivery.NotDelivered)]
    public void RecordDelivery_NotRefunded_KeepsTheCost(SmsDelivery delivery)
    {
        var notification = Settle("Sent");

        notification.RecordDelivery(delivery);

        notification.CostRial.ShouldBe(1_200m);
    }

    [Theory]
    [InlineData(SmsDelivery.BlockedByReceiver)]
    [InlineData(SmsDelivery.Cancelled)]
    public void RecordDelivery_Refunded_MakesTheCostZero(SmsDelivery delivery)
    {
        // §10: the provider gives the cost back for these, so the history's totals leave them out.
        var notification = Settle("Sent");

        notification.RecordDelivery(delivery);

        notification.CostRial.ShouldBe(0m);
        Notification.IsRefunded(delivery).ShouldBeTrue();
    }

    [Fact]
    public void RecordDelivery_NotDeliveredThenCancelled_MakesTheCostZero()
    {
        var notification = Settle("Sent");
        notification.RecordDelivery(SmsDelivery.NotDelivered);

        notification.RecordDelivery(SmsDelivery.Cancelled).IsSuccess.ShouldBeTrue();

        notification.CostRial.ShouldBe(0m);
    }

    [Theory]
    [InlineData(SmsDelivery.BlockedByReceiver, SmsDelivery.Delivered)]
    [InlineData(SmsDelivery.Cancelled, SmsDelivery.NotDelivered)]
    [InlineData(SmsDelivery.Cancelled, SmsDelivery.BlockedByReceiver)]
    public void RecordDelivery_AfterARefund_IsRefused(SmsDelivery refunded, SmsDelivery later)
    {
        // A cost of 0 beside "delivered" would be a message the gym got for free.
        var notification = Settle("Sent");
        notification.RecordDelivery(refunded);

        notification.RecordDelivery(later).Error.ShouldBe(NotificationErrors.DeliveryRefunded);

        notification.Delivery.ShouldBe(refunded);
        notification.CostRial.ShouldBe(0m);
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

    /// <summary>Sent once, then cancelled by the provider, which gave the cost back.</summary>
    private static Notification Cancelled()
    {
        var notification = Settle("Sent");
        notification.RecordDelivery(SmsDelivery.Cancelled).IsSuccess.ShouldBeTrue();

        return notification;
    }

    /// <summary>One automatic resend that the provider cancelled again.</summary>
    private static void CancelAgain(Notification notification)
    {
        notification.PrepareAutoResend().IsSuccess.ShouldBeTrue();
        notification.MarkSent(2, 1_200m, Now).IsSuccess.ShouldBeTrue();
        notification.RecordDelivery(SmsDelivery.Cancelled).IsSuccess.ShouldBeTrue();
    }
}
