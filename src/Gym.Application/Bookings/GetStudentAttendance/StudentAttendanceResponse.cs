using System.Text.Json.Serialization;

using Gym.Domain.Bookings;

namespace Gym.Application.Bookings.GetStudentAttendance;

/// <summary>
/// A student's attendance over a range of dates, a Jalali month on the screen (BUSINESS_RULES.md
/// §15 <i>Attendance for the trainer</i>, task 15.7): each of their sessions up to today, with any
/// trainer, and the totals.
/// </summary>
public sealed record StudentAttendanceResponse(
    Guid MemberId,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<AttendanceItem> Sessions,
    AttendanceTotals Totals);

/// <param name="TrainerName">An earlier trainer's name; <c>null</c> for the trainer asking.</param>
/// <param name="IsVip">Marked VIP by its trainer (15.6e); never for a removed session.</param>
/// <param name="CheckedInAt">The check-in that counted; for an absent student who came after the session, that later one.</param>
/// <param name="CameAfter">Absent, but checked in after the session ended: «بعد از سانس آمد».</param>
public sealed record AttendanceItem(
    DateOnly Date,
    TimeOnly Start,
    TimeOnly End,
    string? TrainerName,
    bool IsVip,
    [property: JsonConverter(typeof(JsonStringEnumConverter<SessionPresence>))] SessionPresence State,
    TimeOnly? CheckedInAt,
    bool CameAfter);

/// <summary>The sessions of the range by state; one not ended yet («هنوز نیامده») is in none.</summary>
public sealed record AttendanceTotals(int OnTime, int Late, int Absent, int Leave, int Cancelled);
