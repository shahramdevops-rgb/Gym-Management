using Gym.Domain.Common;

namespace Gym.Domain.Notifications;

/// <summary>
/// One SMS: who it is for, which event it is about, the template and values it carries, and how
/// sending it went (BUSINESS_RULES.md §10).
/// </summary>
/// <remarks>
/// <para>
/// <b>Written before it is sent.</b> The row is saved as <see cref="NotificationStatus.Pending"/>
/// first, then sent. Because the database allows one row per event (one
/// <see cref="NotificationKind.SubscriptionExpiring"/> per subscription, one birthday per member and
/// Jalali year, one <see cref="NotificationKind.PayableDue"/> per cheque or instalment), a second
/// run, or a second server, cannot write the same event again, so it cannot pay for it twice.
/// </para>
/// <para>
/// <b>The event is a column, not a text.</b> Each kind fills the ids that name its event and leaves
/// the others empty; a check constraint holds that, and the unique indexes are built on those ids.
/// </para>
/// <para>
/// <b>The template and its values are kept.</b> A resend (task 10.5) sends exactly the same message,
/// and the history can show what was sent.
/// </para>
/// </remarks>
public sealed class Notification : Entity
{
    /// <summary>An E.164 number such as <c>+989121234567</c> is at most 16 characters.</summary>
    public const int RecipientMaxLength = 20;

    public const int TemplateNameMaxLength = 100;

    // For EF Core.
    private Notification()
    {
    }

    public NotificationKind Kind { get; private set; }

    /// <summary>E.164: the member's number, or the Owner's for <see cref="NotificationKind.PayableDue"/>.</summary>
    public string Recipient { get; private set; } = string.Empty;

    /// <summary>Set for the three kinds sent to a member; <c>null</c> for <see cref="NotificationKind.PayableDue"/>.</summary>
    public Guid? MemberId { get; private set; }

    /// <summary>Set for <see cref="NotificationKind.SubscriptionExpiring"/> and <see cref="NotificationKind.LowSessions"/>.</summary>
    public Guid? SubscriptionId { get; private set; }

    /// <summary>Set for <see cref="NotificationKind.PayableDue"/>: the cheque or instalment.</summary>
    public Guid? PayableId { get; private set; }

    /// <summary>Set for <see cref="NotificationKind.Birthday"/>: the Jalali year of the birthday, e.g. 1405.</summary>
    public int? JalaliYear { get; private set; }

    /// <summary>The template's name in the provider's panel, as the Owner set it for this kind.</summary>
    public string TemplateName { get; private set; } = string.Empty;

    public string Token { get; private set; } = string.Empty;

    public string? Token2 { get; private set; }

    public string? Token3 { get; private set; }

    public string? Token10 { get; private set; }

    public string? Token20 { get; private set; }

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

    /// <summary>Postgres <c>xmin</c>: two senders cannot both record an outcome for one message.</summary>
    public uint Version { get; private set; }

    /// <summary>The template's values, as one object to hand to the sender.</summary>
    public SmsTokens Tokens => new(Token, Token2, Token3, Token10, Token20);

    /// <summary>The member's subscription is running out (<see cref="NotificationKind.SubscriptionExpiring"/>).</summary>
    public static Notification ForSubscriptionExpiring(
        Guid memberId, Guid subscriptionId, string recipient, string templateName, SmsTokens tokens) =>
        ForSubscription(NotificationKind.SubscriptionExpiring, memberId, subscriptionId, recipient, templateName, tokens);

    /// <summary>The member's subscription has few sessions left (<see cref="NotificationKind.LowSessions"/>).</summary>
    public static Notification ForLowSessions(
        Guid memberId, Guid subscriptionId, string recipient, string templateName, SmsTokens tokens) =>
        ForSubscription(NotificationKind.LowSessions, memberId, subscriptionId, recipient, templateName, tokens);

    /// <param name="jalaliYear">The Jalali year the birthday falls in: at most one per member and year.</param>
    public static Notification ForBirthday(
        Guid memberId, int jalaliYear, string recipient, string templateName, SmsTokens tokens)
    {
        CheckId(memberId, nameof(memberId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(jalaliYear);

        var notification = Create(NotificationKind.Birthday, recipient, templateName, tokens);
        notification.MemberId = memberId;
        notification.JalaliYear = jalaliYear;

        return notification;
    }

    /// <param name="ownerRecipient">The Owner's number from the SMS settings.</param>
    public static Notification ForPayableDue(
        Guid payableId, string ownerRecipient, string templateName, SmsTokens tokens)
    {
        CheckId(payableId, nameof(payableId));

        var notification = Create(NotificationKind.PayableDue, ownerRecipient, templateName, tokens);
        notification.PayableId = payableId;

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

    /// <summary>What the provider said about reaching the phone. It can change while the provider still knows.</summary>
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

        Delivery = delivery;

        return Result.Success();
    }

    private static Notification ForSubscription(
        NotificationKind kind, Guid memberId, Guid subscriptionId, string recipient, string templateName, SmsTokens tokens)
    {
        CheckId(memberId, nameof(memberId));
        CheckId(subscriptionId, nameof(subscriptionId));

        var notification = Create(kind, recipient, templateName, tokens);
        notification.MemberId = memberId;
        notification.SubscriptionId = subscriptionId;

        return notification;
    }

    /// <summary>
    /// The number and the template come from a member row and the settings page, which already
    /// checked them, so anything wrong here is a bug in the caller, not a business failure.
    /// </summary>
    private static Notification Create(NotificationKind kind, string recipient, string templateName, SmsTokens tokens)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        ArgumentNullException.ThrowIfNull(tokens);

        if (recipient.Length > RecipientMaxLength)
        {
            throw new ArgumentException("The number is too long to be a phone number.", nameof(recipient));
        }

        if (templateName.Length > TemplateNameMaxLength)
        {
            throw new ArgumentException("The template name is too long.", nameof(templateName));
        }

        return new Notification
        {
            Kind = kind,
            Recipient = recipient,
            TemplateName = templateName,
            Token = tokens.Token,
            Token2 = tokens.Token2,
            Token3 = tokens.Token3,
            Token10 = tokens.Token10,
            Token20 = tokens.Token20,
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
