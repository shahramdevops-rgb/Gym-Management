namespace Gym.Application.Reports.GetTodaySummary;

/// <summary>
/// Today at a glance, for the Owner's phone (BUSINESS_RULES.md §12 <i>Today's summary</i>, roadmap
/// 13.5): who is inside now and the daily report's figures for today so far.
/// </summary>
/// <param name="Today">The gym's day the figures are for.</param>
/// <param name="InsideNow">Everyone with an open visit, guests included, as the «داخل باشگاه» board lists them.</param>
/// <param name="GuestsInside">Of <paramref name="InsideNow"/>, the guests.</param>
/// <param name="CheckIns">Members' check-ins today, cancelled ones and guests left out (§12 <i>Attendance</i>).</param>
/// <param name="Received">
/// The dashboard's «دریافتی» for today: payments less refunds, without فروشگاه, آنالیز and متفرقه.
/// May be below zero.
/// </param>
/// <param name="PlansSold">Membership plans sold today, cancelled ones left out.</param>
/// <param name="SingleVisitsSold">Single visits (تک‌جلسه‌ای) sold today, cancelled ones left out.</param>
public sealed record TodaySummaryResponse(
    DateOnly Today,
    int InsideNow,
    int GuestsInside,
    int CheckIns,
    decimal Received,
    int PlansSold,
    int SingleVisitsSold);
