using Gym.Domain.Common;

namespace Gym.Domain.Notifications;

/// <summary>
/// One SMS: who it is for, which event it is about, the text it carries, and how sending it went
/// (BUSINESS_RULES.md §10).
/// </summary>
/// <remarks>
/// <para>
/// <b>Written before it is sent.</b> The row is saved as <see cref="NotificationStatus.Pending"/>
/// first, then sent. Because the database allows one row per event (one
/// <see cref="NotificationKind.SubscriptionExpiring"/> per subscription, one birthday per member and
/// Jalali year, one <see cref="NotificationKind.PayableDue"/> per cheque or instalment, one
/// <see cref="NotificationKind.DailyReport"/> per day, one <see cref="NotificationKind.SubscriptionReceipt"/>
/// per subscription, one <see cref="NotificationKind.Welcome"/> per member, one
/// <see cref="NotificationKind.DebtReminder"/> per member and debt, one
/// <see cref="NotificationKind.Absence"/> and one <see cref="NotificationKind.WelcomeBack"/> per member and
/// absence, one
/// <see cref="NotificationKind.Broadcast"/> per member and broadcast), a second
/// run, or a second server, cannot write the same event again, so it cannot pay for it twice.
/// </para>
/// <para>
/// <b>The event is a column, not a text.</b> Each kind fills the ids that name its event and leaves
/// the others empty; a check constraint holds that, and the unique indexes are built on those ids.
/// </para>
/// <para>
/// <b>The text is kept.</b> A resend (task 10.5) sends exactly the same message, and the history
/// shows what was sent.
/// </para>
/// </remarks>
public sealed class Notification : Entity
{
    /// <summary>An E.164 number such as <c>+989121234567</c> is at most 16 characters.</summary>
    public const int RecipientMaxLength = 20;

    public const int TextMaxLength = SmsText.MaxLength;

    /// <summary>
    /// How many times the system resends, by itself, a message the provider cancelled (BUSINESS_RULES.md
    /// §10 <i>Sending</i>, decided with the developer, 1405/08/01, task 10.16). After that only the
    /// Owner's resend is left.
    /// </summary>
    public const int MaxAutoResends = 5;

    // For EF Core.
    private Notification()
    {
    }

    public NotificationKind Kind { get; private set; }

    /// <summary>E.164: the member's number, or the Owner's for <see cref="NotificationKind.PayableDue"/> and <see cref="NotificationKind.DailyReport"/>.</summary>
    public string Recipient { get; private set; } = string.Empty;

    /// <summary>Set for the kinds sent to a member (the debt reminder included); <c>null</c> for the Owner's.</summary>
    public Guid? MemberId { get; private set; }

    /// <summary>
    /// Set for <see cref="NotificationKind.SubscriptionExpiring"/>, <see cref="NotificationKind.LowSessions"/>,
    /// <see cref="NotificationKind.SubscriptionReceipt"/> and <see cref="NotificationKind.Welcome"/> (the
    /// first plan).
    /// </summary>
    public Guid? SubscriptionId { get; private set; }

    /// <summary>Set for <see cref="NotificationKind.PayableDue"/>: the cheque or instalment.</summary>
    public Guid? PayableId { get; private set; }

    /// <summary>Set for <see cref="NotificationKind.Birthday"/>: the Jalali year of the birthday, e.g. 1405.</summary>
    public int? JalaliYear { get; private set; }

    /// <summary>Set for <see cref="NotificationKind.DailyReport"/>: the gym's day the report is about, one per day.</summary>
    public DateOnly? ReportDate { get; private set; }

    /// <summary>
    /// Set for <see cref="NotificationKind.DebtReminder"/>: the gym's day of the oldest unpaid sale the
    /// reminder was about, one per member and debt (task 10.11).
    /// </summary>
    public DateOnly? DebtSince { get; private set; }

    /// <summary>
    /// Set for <see cref="NotificationKind.Absence"/>: the gym's day the member's absence counts from
    /// (their last visit, or their plan's start), one per member and absence (task 10.12). Set for
    /// <see cref="NotificationKind.WelcomeBack"/> too: the absence the member came back from (task 10.20).
    /// </summary>
    public DateOnly? AbsentSince { get; private set; }

    /// <summary>
    /// Set for <see cref="NotificationKind.Broadcast"/>: the <see cref="SmsBroadcast"/> it belongs to,
    /// one per member and broadcast (task 10.13).
    /// </summary>
    public Guid? BroadcastId { get; private set; }

    /// <summary>The whole text, as <see cref="SmsText"/> wrote it when the message was written.</summary>
    public string Text { get; private set; } = string.Empty;

    public NotificationStatus Status { get; private set; }

    /// <summary>How many times a request was made for it: every outcome counts, a give-up does not.</summary>
    public int Attempts { get; private set; }

    /// <summary>A moment (UTC): when the last request was made; <c>null</c> before the first.</summary>
    public DateTimeOffset? LastAttemptAt { get; private set; }

    /// <summary>The provider's code for the last failure; <c>null</c> when none came back.</summary>
    public int? ErrorCode { get; private set; }

    /// <summary>The provider's id for the message, used to ask about its delivery.</summary>
    public long? ProviderMessageId { get; private set; }

    /// <summary>What the provider charged, in Rial as it reports it. Shown in Toman (÷ 10).</summary>
    public decimal? CostRial { get; private set; }

    /// <summary>A moment (UTC); set when it is <see cref="NotificationStatus.Sent"/>.</summary>
    public DateTimeOffset? SentAt { get; private set; }

    /// <summary><c>null</c> until the provider has said whether it reached the phone.</summary>
    public SmsDelivery? Delivery { get; private set; }

    /// <summary>
    /// How many times the system resent it by itself after the provider cancelled it (task 10.16), at
    /// most <see cref="MaxAutoResends"/>. The Owner's resends are not counted.
    /// </summary>
    public int AutoResends { get; private set; }

    /// <summary>
    /// Sent, and then cancelled by the provider (<c>13</c>, a sending error on its side): it never
    /// reached the phone and its cost was given back, so it may be sent again (§10 <i>Sending</i>).
    /// </summary>
    public bool IsCancelledByProvider => Status == NotificationStatus.Sent && Delivery == SmsDelivery.Cancelled;

    /// <summary>Postgres <c>xmin</c>: two senders cannot both record an outcome for one message.</summary>
    public uint Version { get; private set; }

    /// <summary>The member's subscription is running out (<see cref="NotificationKind.SubscriptionExpiring"/>).</summary>
    public static Notification ForSubscriptionExpiring(
        Guid memberId, Guid subscriptionId, string recipient, string text) =>
        ForSubscription(NotificationKind.SubscriptionExpiring, memberId, subscriptionId, recipient, text);

    /// <summary>The member's subscription has few sessions left (<see cref="NotificationKind.LowSessions"/>).</summary>
    public static Notification ForLowSessions(
        Guid memberId, Guid subscriptionId, string recipient, string text) =>
        ForSubscription(NotificationKind.LowSessions, memberId, subscriptionId, recipient, text);

    /// <summary>The member's plan is fully paid (<see cref="NotificationKind.SubscriptionReceipt"/>, task 10.9).</summary>
    public static Notification ForSubscriptionReceipt(
        Guid memberId, Guid subscriptionId, string recipient, string text) =>
        ForSubscription(NotificationKind.SubscriptionReceipt, memberId, subscriptionId, recipient, text);

    /// <summary>The member's first plan was sold (<see cref="NotificationKind.Welcome"/>, task 10.10): at most one per member.</summary>
    /// <param name="subscriptionId">The first plan.</param>
    public static Notification ForWelcome(
        Guid memberId, Guid subscriptionId, string recipient, string text) =>
        ForSubscription(NotificationKind.Welcome, memberId, subscriptionId, recipient, text);

    /// <param name="jalaliYear">The Jalali year the birthday falls in: at most one per member and year.</param>
    public static Notification ForBirthday(
        Guid memberId, int jalaliYear, string recipient, string text)
    {
        CheckId(memberId, nameof(memberId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(jalaliYear);

        var notification = Create(NotificationKind.Birthday, recipient, text);
        notification.MemberId = memberId;
        notification.JalaliYear = jalaliYear;

        return notification;
    }

    /// <param name="ownerRecipient">The Owner's number from the SMS settings.</param>
    public static Notification ForPayableDue(
        Guid payableId, string ownerRecipient, string text)
    {
        CheckId(payableId, nameof(payableId));

        var notification = Create(NotificationKind.PayableDue, ownerRecipient, text);
        notification.PayableId = payableId;

        return notification;
    }

    /// <param name="reportDate">The day the report is about: at most one per day.</param>
    /// <param name="recipient">The daily report's number from the SMS settings.</param>
    public static Notification ForDailyReport(DateOnly reportDate, string recipient, string text)
    {
        var notification = Create(NotificationKind.DailyReport, recipient, text);
        notification.ReportDate = reportDate;

        return notification;
    }

    /// <summary>The member's debt has been owed for a while (<see cref="NotificationKind.DebtReminder"/>, task 10.11).</summary>
    /// <param name="debtSince">The gym's day of the oldest unpaid sale: at most one per member and day.</param>
    public static Notification ForDebtReminder(Guid memberId, DateOnly debtSince, string recipient, string text)
    {
        CheckId(memberId, nameof(memberId));

        var notification = Create(NotificationKind.DebtReminder, recipient, text);
        notification.MemberId = memberId;
        notification.DebtSince = debtSince;

        return notification;
    }

    /// <summary>The member has stopped coming (<see cref="NotificationKind.Absence"/>, task 10.12).</summary>
    /// <param name="absentSince">The gym's day the absence counts from: at most one per member and day.</param>
    public static Notification ForAbsence(Guid memberId, DateOnly absentSince, string recipient, string text)
    {
        CheckId(memberId, nameof(memberId));

        var notification = Create(NotificationKind.Absence, recipient, text);
        notification.MemberId = memberId;
        notification.AbsentSince = absentSince;

        return notification;
    }

    /// <summary>
    /// The member came again after their absence SMS (<see cref="NotificationKind.WelcomeBack"/>, task
    /// 10.20).
    /// </summary>
    /// <param name="absentSince">The day the absence SMS counted from: at most one per member and absence.</param>
    public static Notification ForWelcomeBack(Guid memberId, DateOnly absentSince, string recipient, string text)
    {
        CheckId(memberId, nameof(memberId));

        var notification = Create(NotificationKind.WelcomeBack, recipient, text);
        notification.MemberId = memberId;
        notification.AbsentSince = absentSince;

        return notification;
    }

    /// <summary>The Owner's broadcast to one member (<see cref="NotificationKind.Broadcast"/>, task 10.13).</summary>
    public static Notification ForBroadcast(Guid broadcastId, Guid memberId, string recipient, string text)
    {
        CheckId(broadcastId, nameof(broadcastId));
        CheckId(memberId, nameof(memberId));

        var notification = Create(NotificationKind.Broadcast, recipient, text);
        notification.BroadcastId = broadcastId;
        notification.MemberId = memberId;

        return notification;
    }

    /// <summary>The provider accepted it.</summary>
    public Result MarkSent(long providerMessageId, decimal costRial, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(costRial);

        var attempt = RecordAttempt(now);
        if (attempt.IsFailure)
        {
            return attempt;
        }

        Status = NotificationStatus.Sent;
        ProviderMessageId = providerMessageId;
        CostRial = costRial;
        SentAt = now;
        ErrorCode = null;

        return Result.Success();
    }

    /// <summary>
    /// A failure that may pass (the provider busy, the network down before the request left): it
    /// stays <see cref="NotificationStatus.Pending"/>, to be tried again or given up on.
    /// </summary>
    public Result RecordRetryableFailure(int? errorCode, DateTimeOffset now)
    {
        var attempt = RecordAttempt(now);
        if (attempt.IsFailure)
        {
            return attempt;
        }

        ErrorCode = errorCode;

        return Result.Success();
    }

    /// <summary>A failure that will not pass, or the credit used up: not tried again by itself.</summary>
    public Result MarkFailed(int? errorCode, DateTimeOffset now)
    {
        var attempt = RecordAttempt(now);
        if (attempt.IsFailure)
        {
            return attempt;
        }

        Status = NotificationStatus.Failed;
        ErrorCode = errorCode;

        return Result.Success();
    }

    /// <summary>The request left and no answer came back: it may have been sent and paid for.</summary>
    public Result MarkUnknown(DateTimeOffset now)
    {
        var attempt = RecordAttempt(now);
        if (attempt.IsFailure)
        {
            return attempt;
        }

        Status = NotificationStatus.Unknown;

        return Result.Success();
    }

    /// <summary>
    /// A run stopped between writing this row and saving how its request went (the server restarted):
    /// whether the request left is not known, so it is <see cref="NotificationStatus.Unknown"/> and
    /// never sent again by itself (BUSINESS_RULES.md §10 <i>The daily runs</i>). No attempt is counted,
    /// since none may have been made.
    /// </summary>
    public Result MarkInterrupted()
    {
        if (Status != NotificationStatus.Pending)
        {
            return Result.Failure(NotificationErrors.NotPending);
        }

        Status = NotificationStatus.Unknown;

        return Result.Success();
    }

    /// <summary>
    /// <see cref="NotificationStatus.Failed"/> without another request: no tries left, or the next
    /// one would fall outside the sending hours. The last failure's code stays.
    /// </summary>
    public Result GiveUp()
    {
        if (Status != NotificationStatus.Pending)
        {
            return Result.Failure(NotificationErrors.NotPending);
        }

        Status = NotificationStatus.Failed;

        return Result.Success();
    }

    /// <summary>
    /// The Owner resends a message that failed, whose fate is not known, or that the provider cancelled,
    /// by hand (BUSINESS_RULES.md §10 <i>Sending</i>, tasks 10.5 and 10.16): it is
    /// <see cref="NotificationStatus.Pending"/> again, to be saved before the request like any other, and
    /// the request's outcome is then recorded with the same methods a run uses. The number and the text
    /// do not change: it is exactly the same message.
    /// </summary>
    /// <remarks>
    /// What the last request left is cleared (see <see cref="StartAgain"/>): if the server stops before
    /// the outcome is saved, the row becomes <see cref="NotificationStatus.Unknown"/> at the next run,
    /// and an old code or delivery would then describe a request that is not the last one.
    /// </remarks>
    public Result PrepareResend()
    {
        if (Status is not (NotificationStatus.Failed or NotificationStatus.Unknown) && !IsCancelledByProvider)
        {
            return Result.Failure(NotificationErrors.NotResendable);
        }

        StartAgain();

        return Result.Success();
    }

    /// <summary>
    /// The system resends a message the provider cancelled, by itself (§10 <i>Sending</i>, task 10.16):
    /// the same as <see cref="PrepareResend"/>, for a cancelled message only, at most
    /// <see cref="MaxAutoResends"/> times. When and whether it may go now is the caller's to check.
    /// </summary>
    public Result PrepareAutoResend()
    {
        if (!IsCancelledByProvider)
        {
            return Result.Failure(NotificationErrors.NotCancelledByProvider);
        }

        if (AutoResends >= MaxAutoResends)
        {
            return Result.Failure(NotificationErrors.AutoResendsUsedUp);
        }

        AutoResends++;
        StartAgain();

        return Result.Success();
    }

    /// <summary>
    /// What the provider said about reaching the phone. It can change while the provider still knows,
    /// until the provider gives the cost back (blocked or cancelled, §10 <i>Sending</i>): the cost then
    /// becomes 0, so the history's totals are what the gym really paid, and the delivery stays.
    /// </summary>
    public Result RecordDelivery(SmsDelivery delivery)
    {
        if (!Enum.IsDefined(delivery))
        {
            throw new ArgumentOutOfRangeException(nameof(delivery), delivery, "Unknown delivery.");
        }

        if (Status != NotificationStatus.Sent)
        {
            return Result.Failure(NotificationErrors.NotSent);
        }

        if (Delivery is { } current && IsRefunded(current))
        {
            return Result.Failure(NotificationErrors.DeliveryRefunded);
        }

        Delivery = delivery;
        if (IsRefunded(delivery))
        {
            CostRial = 0m;
        }

        return Result.Success();
    }

    /// <summary>The deliveries for which the provider gives the cost back (§10 <i>Sending</i>).</summary>
    public static bool IsRefunded(SmsDelivery delivery) =>
        delivery is SmsDelivery.BlockedByReceiver or SmsDelivery.Cancelled;

    private static Notification ForSubscription(
        NotificationKind kind, Guid memberId, Guid subscriptionId, string recipient, string text)
    {
        CheckId(memberId, nameof(memberId));
        CheckId(subscriptionId, nameof(subscriptionId));

        var notification = Create(kind, recipient, text);
        notification.MemberId = memberId;
        notification.SubscriptionId = subscriptionId;

        return notification;
    }

    /// <summary>
    /// The number comes from a member row or the settings page, which already checked it, and the
    /// text from <see cref="SmsText"/>, so anything wrong here is a bug in the caller, not a business
    /// failure.
    /// </summary>
    private static Notification Create(NotificationKind kind, string recipient, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (recipient.Length > RecipientMaxLength)
        {
            throw new ArgumentException("The number is too long to be a phone number.", nameof(recipient));
        }

        if (text.Length > TextMaxLength)
        {
            throw new ArgumentException("The text is too long for an SMS.", nameof(text));
        }

        return new Notification
        {
            Kind = kind,
            Recipient = recipient,
            Text = text,
            Status = NotificationStatus.Pending,
        };
    }

    private static void CheckId(Guid id, string name)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("An empty id names no record.", name);
        }
    }

    /// <summary>
    /// Waiting to be sent again. A cancelled message also loses what its last request left: the
    /// provider's id, the cost it gave back, the moment and the delivery belong to a message that never
    /// arrived, and the next request brings its own. The attempts stay counted.
    /// </summary>
    private void StartAgain()
    {
        Status = NotificationStatus.Pending;
        ErrorCode = null;
        ProviderMessageId = null;
        CostRial = null;
        SentAt = null;
        Delivery = null;
    }

    /// <summary>Every outcome of a request starts here: only a message still waiting can have one.</summary>
    private Result RecordAttempt(DateTimeOffset now)
    {
        if (Status != NotificationStatus.Pending)
        {
            return Result.Failure(NotificationErrors.NotPending);
        }

        Attempts++;
        LastAttemptAt = now;

        return Result.Success();
    }
}
