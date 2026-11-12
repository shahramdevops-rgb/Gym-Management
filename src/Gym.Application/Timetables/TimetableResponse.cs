using System.Text.Json.Serialization;

using Gym.Application.Bookings;
using Gym.Application.Common;
using Gym.Domain.Bookings;
using Gym.Domain.Timetables;

namespace Gym.Application.Timetables;

/// <summary>
/// The trainer's «تمام سانس‌های من» (BUSINESS_RULES.md §15 <i>The trainer's sessions</i>): their latest
/// weekly pattern, to edit, and every date of the next 4 weeks with its sessions as they are and the
/// students in each (15.6b).
/// </summary>
/// <param name="LastDay">The timetable's last day, 12 weeks on: later than the last of <paramref name="Days"/>.</param>
/// <param name="Pattern">The pattern saved last, which may start in the future; <c>null</c> before the first.</param>
/// <param name="Days">Today and the days after it, <see cref="TimetableRules.DaysShown"/> in all.</param>
public sealed record TimetableResponse(DateOnly Today, DateOnly LastDay, WeekPatternResponse? Pattern, IReadOnlyList<TimetableDayResponse> Days)
{
    public static async Task<TimetableResponse> ForAsync(IAppDbContext db, Guid trainerId, DateOnly today, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        var lastDay = TimetableRules.LastDay(today);
        var lastShown = today.AddDays(TimetableRules.DaysShown - 1);
        var timetable = await TrainerTimetable.DatesAsync(db, trainerId, today, lastShown, cancellationToken);
        var places = await BookedPlaces.OfTrainerAsync(db, trainerId, today, lastShown, cancellationToken);

        var days = Enumerable.Range(0, TimetableRules.DaysShown)
            .Select(today.AddDays)
            .Select(date =>
            {
                var sessions = timetable.SessionsOn(date)
                    .Select(session => SessionResponse.From(session, BookingRules.In(places, date, session).Count(), timetable.DateNoteOf(date, session)))
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
                    .Select(session => new WeeklySessionResponse(session.Day, session.Start, session.End, session.Capacity, session.IsVip, session.Note))
                    .ToList());

        return new TimetableResponse(today, lastDay, pattern, days);
    }
}

public sealed record WeekPatternResponse(DateOnly StartsOn, IReadOnlyList<WeeklySessionResponse> Sessions);

/// <param name="Note">The standing note («یادداشت ثابت», 15.7e); <c>null</c> for none.</param>
public sealed record WeeklySessionResponse(
    [property: JsonConverter(typeof(JsonStringEnumConverter<DayOfWeek>))] DayOfWeek Day,
    TimeOnly Start,
    TimeOnly End,
    int Capacity,
    bool IsVip,
    string? Note);

/// <param name="ChangedByHand">The trainer changed this date on its own; no sessions then means closed.</param>
public sealed record TimetableDayResponse(DateOnly Date, bool ChangedByHand, IReadOnlyList<SessionResponse> Sessions);

/// <param name="Note">The standing note («یادداشت ثابت», 15.7e), part of the session as its label is; <c>null</c> for none.</param>
/// <param name="Students">The students in it that date (15.6b); above <paramref name="Capacity"/> only when the trainer lowered it.</param>
/// <param name="DateNote">The trainer's note on this date's session alone («یادداشت این روز», 15.7e); <c>null</c> for none.</param>
public sealed record SessionResponse(TimeOnly Start, TimeOnly End, int Capacity, bool IsVip, string? Note, int Students, string? DateNote)
{
    public static SessionResponse From(PlannedSession session, int students, string? dateNote)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new SessionResponse(session.Start, session.End, session.Capacity, session.IsVip, session.Note, students, dateNote);
    }
}
