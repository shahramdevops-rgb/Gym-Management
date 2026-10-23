namespace Gym.Application.Notifications.GetSmsSamples;

/// <summary>One kind's message as it reaches the phone, with a sample name and figures.</summary>
/// <param name="Text">Built by <c>SmsText</c>, the code that writes the real message.</param>
/// <param name="Parts">How many SMS parts it takes, each paid for (<c>SmsParts</c>).</param>
public sealed record SmsSample(string Text, int Parts);

/// <summary>
/// Every kind with a fixed wording, as «تنظیمات پیامک» shows it beside the kind's settings
/// (BUSINESS_RULES.md §10 <i>SMS settings</i>). The broadcast is not here: the Owner writes it.
/// </summary>
public sealed record SmsSamplesResponse(
    SmsSample SubscriptionExpiring,
    SmsSample LowSessions,
    SmsSample Birthday,
    SmsSample PayableDue,
    SmsSample DailyReport,
    SmsSample SubscriptionReceipt,
    SmsSample Welcome,
    SmsSample DebtReminder,
    SmsSample Absence);
