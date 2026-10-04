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
/// <param name="Days">Every day of the range, oldest first; a day with nothing is zeros.</param>
public sealed record MembersReportResponse(
    DateOnly From,
    DateOnly To,
    int Ended,
    int Renewed,
    int Waiting,
    int NewMembers,
    IReadOnlyList<MembersDayResponse> Days);

/// <param name="Ended">Plans whose last day this was, with their renewals as above.</param>
/// <param name="NewMembers">Members whose first plan was sold on this day.</param>
public sealed record MembersDayResponse(DateOnly Date, int Ended, int Renewed, int Waiting, int NewMembers);
