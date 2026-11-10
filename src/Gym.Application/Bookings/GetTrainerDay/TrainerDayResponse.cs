using System.Text.Json.Serialization;

using Gym.Domain.Bookings;

namespace Gym.Application.Bookings.GetTrainerDay;

/// <summary>
/// The trainer's day of attendance (BUSINESS_RULES.md §15 <i>Attendance for the trainer</i>, tasks
/// 15.7 and 15.7d): that day's sessions with each student's state from the desk's check-ins, then
/// the sessions removed since their students were put in them, the day's counts, and the students
/// who came to the gym outside their sessions. Today's page asks again every minute.
/// </summary>
/// <param name="IsToday">The day is the gym's today: its sessions may still be running.</param>
/// <param name="Outside">«آمدند، بدون سانس»: the trainer's students that day whose check-ins count for none of their sessions.</param>
public sealed record TrainerDayResponse(
    DateOnly Date,
    bool IsToday,
    DayTotals Totals,
    IReadOnlyList<DaySession> Sessions,
    IReadOnlyList<OutsideStudent> Outside);

/// <param name="Note">The session's standing note (15.7e); never for a removed session.</param>
/// <param name="DateNote">The trainer's note on this date's session alone (15.7e); never for a removed session.</param>
/// <param name="Removed">The trainer removed the session, or moved its times, after these students were put in it: «لغو شد».</param>
/// <param name="Ended">The session is over: whoever did not come is absent.</param>
public sealed record DaySession(
    TimeOnly Start,
    TimeOnly End,
    bool IsVip,
    string? Note,
    string? DateNote,
    bool Removed,
    bool Started,
    bool Ended,
    IReadOnlyList<DayStudent> Students);

/// <param name="CheckedInAt">The check-in that counted; for a student who came after the session, that later one.</param>
/// <param name="CheckedOutAt">That visit's check-out; <c>null</c> while it is open («داخل است») or when auto-closed.</param>
/// <param name="AutoCheckedOut">The nightly auto-checkout closed that visit: «خروج ثبت نشد».</param>
/// <param name="MinutesEarly">Checked in this many minutes before the start: «۱۰ دقیقه زودتر آمد», still on time.</param>
/// <param name="MinutesLate">Late by this many minutes from the start: «۲۵ دقیقه تأخیر».</param>
/// <param name="HadLeave">The student had leave from the session and came anyway: «مرخصی داشت» (15.7i).</param>
public sealed record DayStudent(
    Guid MemberId,
    string FullName,
    string PhoneNumber,
    [property: JsonConverter(typeof(JsonStringEnumConverter<SessionPresence>))] SessionPresence State,
    TimeOnly? CheckedInAt,
    TimeOnly? CheckedOutAt,
    bool AutoCheckedOut,
    int? MinutesEarly,
    int? MinutesLate,
    bool HadLeave);

/// <summary>
/// The day's counts (15.7d): each student's place in a session once. <paramref name="Present"/> is
/// everyone who came to the session, <paramref name="Late"/> those of them who came late;
/// <paramref name="CameAfter"/> came after it ended, neither present nor absent. «مرخصی» and «لغو شد»
/// are in none, «هنوز نیامده» only in <paramref name="Students"/>.
/// </summary>
public sealed record DayTotals(int Students, int Present, int Late, int CameAfter, int Absent);

/// <summary>A student of the trainer who came to the gym that day outside their sessions.</summary>
public sealed record OutsideStudent(Guid MemberId, string FullName, string PhoneNumber, IReadOnlyList<VisitTimes> Visits);
