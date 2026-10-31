using System.Text.Json.Serialization;

using Gym.Application.Bookings;
using Gym.Application.Common;
using Gym.Domain.Bookings;
using Gym.Domain.Timetables;

namespace Gym.Application.Timetables;

/// <summary>
/// The trainer's «برنامه سانس‌ها» (BUSINESS_RULES.md §15 <i>The trainer's sessions</i>): their latest
/// weekly pattern, to edit, and every date of the timetable's 12 weeks with its sessions as they are
/// and the students in each (15.6b).
/// </summary>
/// <param name="Pattern">The pattern saved last, which may start in the future; <c>null</c> before the first.</param>
/// <param name="Days">Today to <paramref name="LastDay"/>, each one.</param>
public sealed record TimetableResponse(DateOnly Today, DateOnly LastDay, WeekPatternResponse? Pattern, IReadOnlyList<TimetableDayResponse> Days)
{
    public static async Task<TimetableResponse> ForAsync(IAppDbContext db, Guid trainerId, DateOnly today, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        var lastDay = TimetableRules.LastDay(today);
        var timetable = await TrainerTimetable.DatesAsync(db, trainerId, today, lastDay, cancellationToken);
        var places = await BookedPlaces.OfTrainerAsync(db, trainerId, today, lastDay, cancellationToken);

        var days = Enumerable.Range(0, TimetableRules.DaysAhead)
            .Select(today.AddDays)
            .Select(date =>
            {
                var sessions = timetable.SessionsOn(date)
                    .Select(session => SessionResponse.From(session, BookingRules.In(places, date, session).Count()))
                    .ToList();

                return new TimetableDayResponse(date, timetable.Changed.ContainsKey(date), sessions);
            })
            .ToList();

        var latest = timetable.Patterns.MaxBy(pattern => pattern.StartsOn);
        var pattern = latest is null
            ? null
            : new WeekPatternResponse(
                latest.StartsOn,
                latest.Sessions
                    .OrderBy(session => session.Day).ThenBy(session => session.Start).ThenBy(session => session.End)
                    .Select(session => new WeeklySessionResponse(session.Day, session.Start, session.End, session.Capacity))
                    .ToList());

        return new TimetableResponse(today, lastDay, pattern, days);
    }
}

public sealed record WeekPatternResponse(DateOnly StartsOn, IReadOnlyList<WeeklySessionResponse> Sessions);

public sealed record WeeklySessionResponse(
    [property: JsonConverter(typeof(JsonStringEnumConverter<DayOfWeek>))] DayOfWeek Day,
    TimeOnly Start,
    TimeOnly End,
    int Capacity);

/// <param name="ChangedByHand">The trainer changed this date on its own; no sessions then means closed.</param>
public sealed record TimetableDayResponse(DateOnly Date, bool ChangedByHand, IReadOnlyList<SessionResponse> Sessions);

/// <param name="Students">The students in it that date (15.6b); above <paramref name="Capacity"/> only when the trainer lowered it.</param>
public sealed record SessionResponse(TimeOnly Start, TimeOnly End, int Capacity, int Students)
{
    public static SessionResponse From(PlannedSession session, int students)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new SessionResponse(session.Start, session.End, session.Capacity, students);
    }
}
