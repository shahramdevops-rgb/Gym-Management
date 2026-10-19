using Gym.Domain.Common;

namespace Gym.Domain.Notifications;

/// <summary>
/// What the gym sends by SMS, and when (BUSINESS_RULES.md §10 <i>SMS settings</i>). The Owner sets it on «تنظیمات پیامک»; the daily jobs (task 10.3) read it.
/// </summary>
/// <remarks>
/// <para>
/// <b>One row.</b> Seeded by the migration with <see cref="TheId"/>, and a check constraint on the id
/// keeps a second one out, like the price list.
/// </para>
/// <para>
/// <b>Empty and off until the Owner decides.</b> Every SMS costs money, so there are no default
/// numbers: a kind is filled in and then turned on, and it cannot be on while a field is empty. A kind
/// that is off may be left half filled or emptied again; whatever is filled must still be in range.
/// </para>
/// <para>
/// <b>Stored flat.</b> One column per field, so each check constraint reads like the rule it repeats.
/// <see cref="For"/> hands a kind's columns back as one <see cref="SmsKindSettings"/>.
/// </para>
/// </remarks>
public sealed class SmsSettings : Entity
{
    /// <summary>Send times fall on a quarter hour (decided with the developer, task 10.2).</summary>
    public const int SendTimeStepMinutes = 15;

    /// <summary>The id of the one row, seeded by the migration.</summary>
    public static readonly Guid TheId = new("8b2e4f61-3c5a-4d7e-9f10-2a6b8c4d0e5f");

    /// <summary>Nothing is sent before this (Asia/Tehran).</summary>
    public static readonly TimeOnly EarliestSendTime = new(8, 0);

    /// <summary>Nothing is sent after this (Asia/Tehran). 22:00 itself is allowed.</summary>
    public static readonly TimeOnly LatestSendTime = new(22, 0);

    /// <summary>
    /// The daily report goes at this time, fixed in the code, not chosen on the page (decided with
    /// the developer, 1405/07/27, task 10.7).
    /// </summary>
    public static readonly TimeOnly DailyReportSendTime = new(22, 45);

    /// <summary>The daily report's hours run until midnight: the last moment of the gym's day.</summary>
    public static readonly TimeOnly DailyReportLatestSendTime = TimeOnly.MaxValue;

    // For EF Core. The row comes from the migration, so no code creates one.
    private SmsSettings()
    {
    }

    /// <summary>The switch for every SMS. Off stops them all, whatever each kind says.</summary>
    public bool Enabled { get; private set; }

    public bool SubscriptionExpiringEnabled { get; private set; }

    /// <summary>Days before the subscription's <c>EndDate</c>, today included.</summary>
    public int? SubscriptionExpiringDaysBefore { get; private set; }

    public TimeOnly? SubscriptionExpiringSendTime { get; private set; }

    public bool LowSessionsEnabled { get; private set; }

    /// <summary>Sent once the sessions left are at most this.</summary>
    public int? LowSessionsThreshold { get; private set; }

    public TimeOnly? LowSessionsSendTime { get; private set; }

    public bool BirthdayEnabled { get; private set; }

    /// <summary>Days before the birthday; 0 is the day itself.</summary>
    public int? BirthdayDaysBefore { get; private set; }

    public TimeOnly? BirthdaySendTime { get; private set; }

    public bool PayableDueEnabled { get; private set; }

    /// <summary>Days before the cheque's or instalment's date; 0 is the day itself.</summary>
    public int? PayableDueDaysBefore { get; private set; }

    public TimeOnly? PayableDueSendTime { get; private set; }

    /// <summary>Where the cheque and instalment reminders go, in E.164 (<c>+989…</c>).</summary>
    public string? OwnerPhone { get; private set; }

    /// <summary>The Owner's report of the day (§10 <i>The daily report</i>). It has no number and no time to choose.</summary>
    public bool DailyReportEnabled { get; private set; }

    /// <summary>
    /// Where the daily report goes, in E.164: its own number, apart from <see cref="OwnerPhone"/>, so
    /// the report can go to someone else (the developer, 1405/07/27).
    /// </summary>
    public string? DailyReportPhone { get; private set; }

    /// <summary>Postgres <c>xmin</c>, so a stale edit cannot overwrite a newer one.</summary>
    public uint Version { get; private set; }

    /// <summary>
    /// The window a send time may be chosen in: 08:00–22:00, both included (§10 <i>SMS settings</i>).
    /// The messages themselves follow <see cref="IsWithinSendingHours(NotificationKind, TimeOnly)"/>.
    /// </summary>
    public static bool IsWithinSendingHours(TimeOnly localTime) =>
        localTime >= EarliestSendTime && localTime <= LatestSendTime;

    /// <summary>
    /// Whether a message of this kind may be sent now (§10 <i>Sending</i>): 08:00–22:00, and for the
    /// daily report 08:00 until midnight, so a retry or a missed 22:45 run can still be made that night.
    /// </summary>
    public static bool IsWithinSendingHours(NotificationKind kind, TimeOnly localTime) =>
        localTime >= EarliestSendTime && localTime <= LatestSendTimeFor(kind);

    /// <summary>The last moment a message of this kind may be sent, a retry included.</summary>
    public static TimeOnly LatestSendTimeFor(NotificationKind kind) =>
        kind == NotificationKind.DailyReport ? DailyReportLatestSendTime : LatestSendTime;

    /// <summary>The smallest and largest number each kind accepts (§10, the settings table).</summary>
    public static (int Min, int Max) ThresholdRange(NotificationKind kind) => kind switch
    {
        NotificationKind.SubscriptionExpiring => (1, 30),
        NotificationKind.LowSessions => (1, 10),
        NotificationKind.Birthday => (0, 7),
        NotificationKind.PayableDue => (0, 30),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No range is set for this kind."),
    };

    /// <summary>One kind's settings, as the page and the daily job see them.</summary>
    public SmsKindSettings For(NotificationKind kind) => kind switch
    {
        NotificationKind.SubscriptionExpiring => new(
            SubscriptionExpiringEnabled, SubscriptionExpiringDaysBefore, SubscriptionExpiringSendTime),
        NotificationKind.LowSessions => new(
            LowSessionsEnabled, LowSessionsThreshold, LowSessionsSendTime),
        NotificationKind.Birthday => new(
            BirthdayEnabled, BirthdayDaysBefore, BirthdaySendTime),
        NotificationKind.PayableDue => new(
            PayableDueEnabled, PayableDueDaysBefore, PayableDueSendTime),

        // No number, and the time is fixed: the run is scheduled from it like any other kind's.
        NotificationKind.DailyReport => new(
            DailyReportEnabled, Threshold: null, DailyReportSendTime),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown notification kind."),
    };

    /// <summary>
    /// Saves the whole page at once. Every field is checked first and nothing changes on a refusal, so
    /// a half-saved page never exists.
    /// </summary>
    /// <param name="ownerPhone">Already normalized to E.164 by the caller, or <c>null</c>.</param>
    /// <param name="dailyReportPhone">The same, for the daily report.</param>
    public Result Update(
        bool enabled,
        SmsKindSettings subscriptionExpiring,
        SmsKindSettings lowSessions,
        SmsKindSettings birthday,
        SmsKindSettings payableDue,
        string? ownerPhone,
        bool dailyReportEnabled,
        string? dailyReportPhone)
    {
        ArgumentNullException.ThrowIfNull(subscriptionExpiring);
        ArgumentNullException.ThrowIfNull(lowSessions);
        ArgumentNullException.ThrowIfNull(birthday);
        ArgumentNullException.ThrowIfNull(payableDue);

        var phone = Normalized(ownerPhone, nameof(ownerPhone));
        var reportPhone = Normalized(dailyReportPhone, nameof(dailyReportPhone));

        var error = Check(NotificationKind.SubscriptionExpiring, subscriptionExpiring, phone)
            ?? Check(NotificationKind.LowSessions, lowSessions, phone)
            ?? Check(NotificationKind.Birthday, birthday, phone)
            ?? Check(NotificationKind.PayableDue, payableDue, phone)
            ?? CheckDailyReportComplete(dailyReportEnabled, reportPhone);
        if (error is not null)
        {
            return Result.Failure(error);
        }

        Enabled = enabled;

        SubscriptionExpiringEnabled = subscriptionExpiring.Enabled;
        SubscriptionExpiringDaysBefore = subscriptionExpiring.Threshold;
        SubscriptionExpiringSendTime = subscriptionExpiring.SendTime;

        LowSessionsEnabled = lowSessions.Enabled;
        LowSessionsThreshold = lowSessions.Threshold;
        LowSessionsSendTime = lowSessions.SendTime;

        BirthdayEnabled = birthday.Enabled;
        BirthdayDaysBefore = birthday.Threshold;
        BirthdaySendTime = birthday.SendTime;

        PayableDueEnabled = payableDue.Enabled;
        PayableDueDaysBefore = payableDue.Threshold;
        PayableDueSendTime = payableDue.SendTime;

        OwnerPhone = phone;

        DailyReportEnabled = dailyReportEnabled;
        DailyReportPhone = reportPhone;

        return Result.Success();
    }

    // ---- The checks, shared with the Application validator so the form hears the same answer ----

    /// <summary>An empty number is allowed (the kind may be off); a filled one must be in the kind's range.</summary>
    public static Error? CheckThreshold(NotificationKind kind, int? threshold)
    {
        if (threshold is null)
        {
            return null;
        }

        var (min, max) = ThresholdRange(kind);
        if (threshold >= min && threshold <= max)
        {
            return null;
        }

        return kind switch
        {
            NotificationKind.SubscriptionExpiring => SmsSettingsErrors.SubscriptionExpiringDaysOutOfRange,
            NotificationKind.LowSessions => SmsSettingsErrors.LowSessionsOutOfRange,
            NotificationKind.Birthday => SmsSettingsErrors.BirthdayDaysOutOfRange,
            _ => SmsSettingsErrors.PayableDueDaysOutOfRange,
        };
    }

    /// <summary>Between 08:00 and 22:00, both included, on a quarter hour with no seconds.</summary>
    public static Error? CheckSendTime(TimeOnly? sendTime)
    {
        if (sendTime is not { } time)
        {
            return null;
        }

        var inWindow = IsWithinSendingHours(time);
        var onQuarter = time.Minute % SendTimeStepMinutes == 0 && time.Second == 0 && time.Millisecond == 0
            && time.Microsecond == 0 && time.Nanosecond == 0;

        return inWindow && onQuarter ? null : SmsSettingsErrors.SendTimeOutOfRange;
    }

    /// <summary>A kind that is on has every field filled; the cheque reminders also need the Owner's number.</summary>
    public static Error? CheckComplete(NotificationKind kind, SmsKindSettings settings, string? ownerPhone)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.Enabled)
        {
            return null;
        }

        var filled = settings.IsFilled()
            && (kind != NotificationKind.PayableDue || !string.IsNullOrWhiteSpace(ownerPhone));

        return filled ? null : SmsSettingsErrors.SettingsIncomplete;
    }

    /// <summary>The daily report has only a number to fill: on means the number is there.</summary>
    public static Error? CheckDailyReportComplete(bool enabled, string? phone) =>
        !enabled || !string.IsNullOrWhiteSpace(phone) ? null : SmsSettingsErrors.SettingsIncomplete;

    private static string? Normalized(string? phone, string parameterName)
    {
        var value = string.IsNullOrWhiteSpace(phone) ? null : phone;
        if (value is not null && !IsIranianMobile(value))
        {
            // A caller that skipped IPhoneNormalizer is a bug, not bad input from a user.
            throw new ArgumentException("A number must already be normalized to E.164.", parameterName);
        }

        return value;
    }

    private static Error? Check(NotificationKind kind, SmsKindSettings settings, string? ownerPhone) =>
        CheckThreshold(kind, settings.Threshold)
        ?? CheckSendTime(settings.SendTime)
        ?? CheckComplete(kind, settings, ownerPhone);

    /// <summary>What <c>IPhoneNormalizer</c> makes of an Iranian mobile: <c>+989</c> and nine digits.</summary>
    private static bool IsIranianMobile(string phone) =>
        phone.Length == 13 && phone.StartsWith("+989", StringComparison.Ordinal) && phone[1..].All(char.IsAsciiDigit);
}
