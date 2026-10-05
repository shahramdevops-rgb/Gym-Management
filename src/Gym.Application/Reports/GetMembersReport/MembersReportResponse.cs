namespace Gym.Application.Reports.GetMembersReport;

/// <summary>
/// Renewals and new members over a range (BUSINESS_RULES.md §12 <i>Operational reports</i>). Day
/// by day, so the dashboard can add the days up into Jalali months: the server keeps to Gregorian
/// dates (§13).
/// </summary>
/// <param name="Ended">Plans whose last day is in the range and already behind us.</param>
/// <param name="Renewed">Of <paramref name="Ended"/>, those followed by another plan sold within 30 days of the end.</param>
/// <param name="Waiting">
/// Of <paramref name="Ended"/>, those not renewed yet that still have time to be. The rate is
/// <paramref name="Renewed"/> ÷ (<paramref name="Ended"/> − <paramref name="Waiting"/>).
/// </param>
/// <param name="NewMembers">Members whose first plan was sold in the range.</param>
/// <param name="Trials">
/// New people who bought a single visit in the range: no membership plan sold to them before it.
/// Each person once, by their first single visit of the range.
/// </param>
/// <param name="TrialsConverted">Of <paramref name="Trials"/>, those sold a membership plan within 30 days of it.</param>
/// <param name="TrialsWaiting">
/// Of <paramref name="Trials"/>, those not converted yet whose 30 days are not over. The rate is
/// <paramref name="TrialsConverted"/> ÷ (<paramref name="Trials"/> − <paramref name="TrialsWaiting"/>).
/// </param>
/// <param name="Days">Every day of the range, oldest first; a day with nothing is zeros.</param>
public sealed record MembersReportResponse(
    DateOnly From,
    DateOnly To,
    int Ended,
    int Renewed,
    int Waiting,
    int NewMembers,
    int Trials,
    int TrialsConverted,
    int TrialsWaiting,
    IReadOnlyList<MembersDayResponse> Days);

/// <param name="Ended">Plans whose last day this was, with their renewals as above.</param>
/// <param name="NewMembers">Members whose first plan was sold on this day.</param>
public sealed record MembersDayResponse(DateOnly Date, int Ended, int Renewed, int Waiting, int NewMembers);
