namespace Gym.Domain.Notifications;

/// <summary>
/// What the Owner's daily report says about one day (BUSINESS_RULES.md §10 <i>The daily report</i>):
/// the dashboard's figures for that day, read by the run and written out by <see cref="SmsText.ForDailyReport"/>.
/// </summary>
/// <param name="Day">The gym's day the figures are about.</param>
/// <param name="CheckIns">Members' check-ins, cancelled ones and guests left out.</param>
/// <param name="PlansSold">Membership plans sold that day, cancelled ones left out.</param>
/// <param name="SingleVisitsSold">Single visits (تک‌جلسه‌ای) sold that day, cancelled ones left out.</param>
/// <param name="Received">
/// «دریافتی»: payments less refunds, without فروشگاه, آنالیز and متفرقه, in Toman. May be below zero.
/// </param>
/// <param name="Cash">The cash part of <paramref name="Received"/>.</param>
/// <param name="Card">The card part of <paramref name="Received"/>.</param>
/// <param name="BankTransfer">The bank transfer part of <paramref name="Received"/>.</param>
public sealed record DailyReportFigures(
    DateOnly Day,
    int CheckIns,
    int PlansSold,
    int SingleVisitsSold,
    decimal Received,
    decimal Cash,
    decimal Card,
    decimal BankTransfer)
{
    /// <summary>
    /// Every figure is zero: no check-in, nothing sold, no money in or out. Such a day is not sent
    /// (decided with the developer, 1405/07/27): an SMS of zeros is paid for and says nothing.
    /// </summary>
    public bool IsEmpty() =>
        CheckIns == 0 && PlansSold == 0 && SingleVisitsSold == 0
        && Received == 0m && Cash == 0m && Card == 0m && BankTransfer == 0m;
}
