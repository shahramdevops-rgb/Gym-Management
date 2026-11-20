using Gym.Application.Bookings.GetStudentAttendance;

namespace Gym.Application.Bookings.GetMyAttendance;

/// <summary>
/// The member's own attendance over a range of dates, a Jalali month on the screen (BUSINESS_RULES.md
/// §15 <i>The member's own attendance</i>, task 15.7j): the same sessions and totals their trainer
/// sees, and since 15.7p their «روز دیگر» visits too.
/// </summary>
/// <param name="Sessions">Each session up to today; <see cref="AttendanceItem.TrainerName"/> names a trainer other than the member's trainer now.</param>
/// <param name="OtherDays">Every check-in of the range on a day with no session to come to, «روز دیگر».</param>
public sealed record MyAttendanceResponse(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<AttendanceItem> Sessions,
    AttendanceTotals Totals,
    IReadOnlyList<OtherDayVisit> OtherDays);
