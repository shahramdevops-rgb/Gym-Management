using Gym.Domain.Notifications;

namespace Gym.Application.Notifications.UpdateSmsSettings;

/// <param name="OwnerPhone">As typed (any digits, <c>09…</c> or <c>+98…</c>); empty clears it.</param>
/// <param name="DailyReport">The daily report's switch and number (task 10.7).</param>
/// <param name="SubscriptionReceipt">The receipt's switch (task 10.9).</param>
/// <param name="Welcome">The welcome's switch (task 10.10).</param>
/// <param name="DebtReminder">The debt reminder's switch, days, time and minimum amount (task 10.11).</param>
/// <param name="Version">The <c>version</c> from the settings as they were read before editing.</param>
public sealed record UpdateSmsSettingsCommand(
    bool Enabled,
    SmsKindSettings SubscriptionExpiring,
    SmsKindSettings LowSessions,
    SmsKindSettings Birthday,
    SmsKindSettings PayableDue,
    string? OwnerPhone,
    DailyReportSettings DailyReport,
    SubscriptionReceiptSettings SubscriptionReceipt,
    WelcomeSettings Welcome,
    DebtReminderSettings DebtReminder,
    uint Version);
