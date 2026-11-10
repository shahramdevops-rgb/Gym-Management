using System.Text.Json.Serialization;

using Gym.Domain.Bookings;

namespace Gym.Application.Bookings.GetStudentAttendance;

/// <summary>
/// A student's attendance over a range of dates, a Jalali month on the screen (BUSINESS_RULES.md
/// §15 <i>Attendance for the trainer</i>, task 15.7): each of their sessions up to today, with any
/// trainer, and the totals; and the visits outside their sessions (15.7d).
/// </summary>
/// <param name="Outside">«خارج از سانس»: check-ins that count for none of the student's sessions, from the day they became this trainer's student.</param>
public sealed record StudentAttendanceResponse(
    Guid MemberId,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<AttendanceItem> Sessions,
    AttendanceTotals Totals,
    IReadOnlyList<OutsideVisit> Outside);

/// <param name="TrainerName">An earlier trainer's name; <c>null</c> for the trainer asking.</param>
/// <param name="IsVip">Marked VIP by its trainer (15.6e); never for a removed session.</param>
/// <param name="CheckedInAt">The check-in that counted; for a student who came after the session, that later one.</param>
/// <param name="CheckedOutAt">That visit's check-out; <c>null</c> while it is open («داخل است») or when auto-closed.</param>
/// <param name="AutoCheckedOut">The nightly auto-checkout closed that visit: «خروج ثبت نشد».</param>
/// <param name="MinutesEarly">Checked in this many minutes before the start, still on time.</param>
/// <param name="MinutesLate">Late by this many minutes from the start.</param>
/// <param name="HadLeave">The student had leave from the session and came anyway: «مرخصی داشت» (15.7i).</param>
public sealed record AttendanceItem(
    DateOnly Date,
    TimeOnly Start,
    TimeOnly End,
    string? TrainerName,
    bool IsVip,
    [property: JsonConverter(typeof(JsonStringEnumConverter<SessionPresence>))] SessionPresence State,
    TimeOnly? CheckedInAt,
    TimeOnly? CheckedOutAt,
    bool AutoCheckedOut,
    int? MinutesEarly,
    int? MinutesLate,
    bool HadLeave);

/// <summary>A visit to the gym outside the student's sessions, on <paramref name="Date"/>.</summary>
public sealed record OutsideVisit(DateOnly Date, VisitTimes Visit);

/// <summary>The sessions of the range by state; one not ended yet («هنوز نیامده») is in none.</summary>
public sealed record AttendanceTotals(int OnTime, int Late, int CameAfter, int Absent, int Leave, int Cancelled);
