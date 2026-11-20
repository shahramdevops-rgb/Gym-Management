using System.Text.Json.Serialization;

using Gym.Domain.Bookings;

namespace Gym.Application.Bookings.GetStudentAttendance;

/// <summary>
/// A student's attendance over a range of dates, a Jalali month on the screen (BUSINESS_RULES.md
/// §15 <i>Attendance for the trainer</i>, task 15.7): each of their sessions up to today, with any
/// trainer, and the totals; and since 15.7p the visits on days with no session to come to.
/// </summary>
/// <param name="OtherDays">
/// «روز دیگر»: the check-ins on a day the student had no session to come to (none, or only a
/// cancelled one or one they had leave from), from the day they became this trainer's student.
/// </param>
public sealed record StudentAttendanceResponse(
    Guid MemberId,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<AttendanceItem> Sessions,
    AttendanceTotals Totals,
    IReadOnlyList<OtherDayVisit> OtherDays);

/// <param name="TrainerName">An earlier trainer's name; <c>null</c> for the trainer asking.</param>
/// <param name="IsVip">Marked VIP by its trainer (15.6e); never for a removed session.</param>
/// <param name="CheckedInAt">The check-in that counted; for «خارج از سانس», the one at another hour that day (15.7p).</param>
/// <param name="CheckedOutAt">That visit's check-out; <c>null</c> while it is open («داخل است») or when auto-closed.</param>
/// <param name="AutoCheckedOut">The nightly auto-checkout closed that visit: «خروج ثبت نشد».</param>
/// <param name="MinutesEarly">«زودتر از موعد» by this many minutes from the start (15.7m).</param>
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

/// <summary>A visit to the gym on <paramref name="Date"/>, a day the student had no session to come to: «روز دیگر» (15.7p).</summary>
public sealed record OtherDayVisit(DateOnly Date, VisitTimes Visit);

/// <summary>
/// The month's eight boxes (15.7p): the sessions of the range by state, one not ended yet («هنوز
/// نیامده») in none; and «روز دیگر», the visits on days with no session to come to (two on one day
/// count two). <paramref name="Early"/>: «زودتر از موعد» (15.7m); <paramref name="OutsideSession"/>:
/// «خارج از سانس».
/// </summary>
public sealed record AttendanceTotals(
    int Early,
    int OnTime,
    int Late,
    int OutsideSession,
    int Absent,
    int Leave,
    int Cancelled,
    int OtherDayVisits);
