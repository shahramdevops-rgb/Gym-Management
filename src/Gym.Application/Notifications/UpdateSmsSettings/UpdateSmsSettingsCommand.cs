using Gym.Domain.Notifications;

namespace Gym.Application.Notifications.UpdateSmsSettings;

/// <param name="OwnerPhone">As typed (any digits, <c>09…</c> or <c>+98…</c>); empty clears it.</param>
/// <param name="DailyReport">The daily report's switch, number and time (tasks 10.7 and 10.17).</param>
/// <param name="LowSessions">«جلسات رو به اتمام»: its switch, time and three rows by the plan's size (task 10.17).</param>
/// <param name="SubscriptionReceipt">The receipt's switch (task 10.9).</param>
/// <param name="Welcome">The welcome's switch (task 10.10).</param>
/// <param name="WelcomeBack">The welcome back's switch (task 10.20).</param>
/// <param name="DebtReminder">The debt reminder's switch, days, time and minimum amount (task 10.11).</param>
/// <param name="Absence">The absence SMS's switch, days away and time (task 10.12).</param>
/// <param name="Broadcast">The broadcast's switch (task 10.13).</param>
/// <param name="TrainerRequest">The trainer's SMS switch (task 15.4c).</param>
/// <param name="Version">The <c>version</c> from the settings as they were read before editing.</param>
public sealed record UpdateSmsSettingsCommand(
    bool Enabled,
    SmsKindSettings SubscriptionExpiring,
    LowSessionsSettings LowSessions,
    SmsKindSettings Birthday,
    SmsKindSettings PayableDue,
    string? OwnerPhone,
    DailyReportSettings DailyReport,
    SubscriptionReceiptSettings SubscriptionReceipt,
    WelcomeSettings Welcome,
    WelcomeBackSettings WelcomeBack,
    DebtReminderSettings DebtReminder,
    SmsKindSettings Absence,
    BroadcastSettings Broadcast,
    TrainerRequestSettings TrainerRequest,
    uint Version);
