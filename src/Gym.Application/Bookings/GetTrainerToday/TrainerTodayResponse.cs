using System.Text.Json.Serialization;

using Gym.Domain.Bookings;

namespace Gym.Application.Bookings.GetTrainerToday;

/// <summary>
/// The trainer's «امروز» (BUSINESS_RULES.md §15 <i>Attendance for the trainer</i>, task 15.7):
/// today's sessions with each student's state from the desk's check-ins, then the sessions removed
/// since their students were put in them. The page asks again every minute.
/// </summary>
public sealed record TrainerTodayResponse(DateOnly Date, IReadOnlyList<TodaySession> Sessions);

/// <param name="Removed">The trainer removed the session, or moved its times, after these students were put in it: «لغو شد».</param>
/// <param name="Ended">The session is over: whoever did not come is absent.</param>
public sealed record TodaySession(
    TimeOnly Start,
    TimeOnly End,
    bool IsVip,
    bool Removed,
    bool Started,
    bool Ended,
    IReadOnlyList<TodayStudent> Students);

/// <param name="CheckedInAt">The check-in that counted; for an absent student who came after the session, that later one.</param>
/// <param name="CameAfter">Absent, but checked in after the session ended: «بعد از سانس آمد».</param>
public sealed record TodayStudent(
    Guid MemberId,
    string FullName,
    string PhoneNumber,
    [property: JsonConverter(typeof(JsonStringEnumConverter<SessionPresence>))] SessionPresence State,
    TimeOnly? CheckedInAt,
    bool CameAfter);
