namespace Gym.Application.Bookings.CheckStudentSchedule;

/// <param name="Sessions">The dates the schedule puts the student in a session.</param>
/// <param name="Full">Those with no room; the schedule cannot be saved while there is one.</param>
public sealed record ScheduleCheckResponse(int Sessions, IReadOnlyList<SessionDateResponse> Full);
