using Gym.Application.Bookings.GetStudentAttendance;

namespace Gym.Application.Bookings.GetMyAttendance;

/// <summary>
/// The member's own attendance over a range of dates, a Jalali month on the screen (BUSINESS_RULES.md
/// §15 <i>The member's own attendance</i>, task 15.7j): the same sessions and totals their trainer
/// sees, without «خارج از سانس».
/// </summary>
/// <param name="Sessions">Each session up to today; <see cref="AttendanceItem.TrainerName"/> names a trainer other than the member's trainer now.</param>
public sealed record MyAttendanceResponse(DateOnly From, DateOnly To, IReadOnlyList<AttendanceItem> Sessions, AttendanceTotals Totals);
