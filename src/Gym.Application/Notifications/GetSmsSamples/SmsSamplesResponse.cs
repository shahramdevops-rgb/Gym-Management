namespace Gym.Application.Notifications.GetSmsSamples;

/// <summary>One kind's message as it reaches the phone, with a sample name and figures.</summary>
/// <param name="Text">Built by <c>SmsText</c>, the code that writes the real message.</param>
/// <param name="Parts">How many SMS parts it takes, each paid for (<c>SmsParts</c>).</param>
public sealed record SmsSample(string Text, int Parts);

/// <summary>
/// Every kind with a fixed wording, as «تنظیمات پیامک» shows it beside the kind's settings
/// (BUSINESS_RULES.md §10 <i>SMS settings</i>). The broadcast is not here: the Owner writes it.
/// The welcome back has its six texts, in the order a member gets them (task 10.20). The trainer's
/// SMS (task 15.4c) names a sample trainer and member.
/// </summary>
public sealed record SmsSamplesResponse(
    SmsSample SubscriptionExpiring,
    SmsSample LowSessions,
    SmsSample Birthday,
    SmsSample PayableDue,
    SmsSample DailyReport,
    SmsSample SubscriptionReceipt,
    SmsSample Welcome,
    IReadOnlyList<SmsSample> WelcomeBack,
    SmsSample DebtReminder,
    SmsSample Absence,
    SmsSample TrainerRequest);
