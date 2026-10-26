using Gym.Application.Common;
using Gym.Domain.Notifications;
using Gym.Domain.Payables;

namespace Gym.Application.Notifications.GetSmsSamples;

/// <summary>
/// Each kind's message with a sample member and sample figures (BUSINESS_RULES.md §10 <i>SMS
/// settings</i>), so the Owner sees exactly what goes out before turning a kind on. Owner only.
/// </summary>
/// <remarks>
/// Built by <see cref="SmsText"/>, the same code the runs use, so the page can never show a wording
/// the members do not get. The frontend keeps no copy of the texts. Nothing is read from the
/// database: the figures are examples, the dates a few days from today.
/// </remarks>
public sealed class GetSmsSamplesHandler(IGymCalendar calendar)
{
    /// <summary>The sample member: «سید» is skipped, as in every real message.</summary>
    public const string SampleMemberName = "سید علی رضایی";

    public SmsSamplesResponse Handle()
    {
        var today = calendar.Today();

        return new SmsSamplesResponse(
            SubscriptionExpiring: Sample(SmsText.ForRunningOut(SampleMemberName, today.AddDays(3), sessionsLeft: 4)),
            LowSessions: Sample(SmsText.ForFewSessionsLeft(SampleMemberName, sessionsLeft: 2)),
            Birthday: Sample(SmsText.ForBirthday(SampleMemberName, today.AddDays(3))),
            PayableDue: Sample(SmsText.ForPayableDue(PayableKind.Cheque, 12_500_000m, today.AddDays(2), "فروشگاه تجهیزات ورزشی")),
            DailyReport: Sample(SmsText.ForDailyReport(new DailyReportFigures(
                Day: today,
                CheckIns: 48,
                PlansSold: 3,
                SingleVisitsSold: 5,
                Received: 7_850_000m,
                Cash: 1_200_000m,
                Card: 5_150_000m,
                BankTransfer: 1_500_000m))),
            SubscriptionReceipt: Sample(SmsText.ForSubscriptionReceipt(SampleMemberName, sessions: 12, today, today.AddDays(30), 1_800_000m)),
            Welcome: Sample(SmsText.ForWelcome(SampleMemberName)),
            WelcomeBack: [.. Enumerable.Range(0, SmsText.WelcomeBackTexts.Count)
                .Select(turn => Sample(SmsText.ForWelcomeBack(SampleMemberName, turn)))],
            DebtReminder: Sample(SmsText.ForDebtReminder(SampleMemberName, 450_000m)),
            Absence: Sample(SmsText.ForAbsence(SampleMemberName, daysAway: 12)));
    }

    private static SmsSample Sample(string text) => new(text, SmsParts.Count(text));
}
