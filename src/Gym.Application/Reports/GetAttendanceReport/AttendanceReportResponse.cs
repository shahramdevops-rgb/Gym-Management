using System.Text.Json.Serialization;

namespace Gym.Application.Reports.GetAttendanceReport;

/// <summary>
/// Members' visits over a range (BUSINESS_RULES.md §12 <i>Operational reports</i>). Cancelled
/// check-ins and guests are not counted; a cardio-only visit is.
/// </summary>
/// <param name="Visits">Check-ins in the range.</param>
/// <param name="PreviousVisits">Check-ins in the range of the same length ending the day before it.</param>
/// <param name="Members">How many different members came in the range.</param>
/// <param name="Days">Every day of the range, oldest first; a day with nothing is a zero.</param>
/// <param name="ByWeekday">
/// The seven weekdays, Saturday first as the Iranian week runs, each with its 24 hours.
/// </param>
public sealed record AttendanceReportResponse(
    DateOnly From,
    DateOnly To,
    int Visits,
    int PreviousVisits,
    int Members,
    IReadOnlyList<AttendanceDayResponse> Days,
    IReadOnlyList<AttendanceWeekdayResponse> ByWeekday);

public sealed record AttendanceDayResponse(DateOnly Date, int Visits);

/// <param name="Hours">Check-ins by their hour in the gym's time zone: index 0 is 00:00 to 00:59.</param>
public sealed record AttendanceWeekdayResponse(
    [property: JsonConverter(typeof(JsonStringEnumConverter<DayOfWeek>))] DayOfWeek Weekday,
    IReadOnlyList<int> Hours);
