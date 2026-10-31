using System.Text.Json.Serialization;

using Gym.Application.Timetables;
using Gym.Domain.Bookings;

namespace Gym.Application.Bookings;

/// <summary>A date a student is in one of the trainer's sessions (BUSINESS_RULES.md §15 <i>Students in sessions</i>).</summary>
/// <param name="Moved">A date moved to, not one of the fixed schedule's.</param>
/// <param name="Removed">The trainer has since removed the session or moved its times: «سانس برداشته شد».</param>
/// <param name="Started">It has started: it can no longer be left or moved.</param>
public sealed record PlaceResponse(DateOnly Date, TimeOnly Start, TimeOnly End, bool Moved, bool Removed, bool Started)
{
    internal static PlaceResponse From(BookedPlace place, TimetableDates timetable, DateOnly today, TimeOnly timeOfDay) => new(
        place.Date,
        place.Start,
        place.End,
        place.IsMoved,
        BookingRules.SessionOf(place, timetable.SessionsOn(place.Date)) is null,
        BookingRules.HasStarted(place.Date, place.Start, today, timeOfDay));
}

/// <summary>A student's «برنامهٔ ثابت»: the weekly sessions, from <paramref name="StartsOn"/> to <paramref name="EndsOn"/>.</summary>
public sealed record FixedScheduleResponse(DateOnly StartsOn, DateOnly EndsOn, IReadOnlyList<WeeklySlotResponse> Slots)
{
    public static FixedScheduleResponse From(FixedSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        var slots = schedule.Slots
            .OrderBy(slot => slot.Day).ThenBy(slot => slot.Start).ThenBy(slot => slot.End)
            .Select(slot => new WeeklySlotResponse(slot.Day, slot.Start, slot.End))
            .ToList();

        return new FixedScheduleResponse(schedule.StartsOn, schedule.EndsOn, slots);
    }
}

public sealed record WeeklySlotResponse(
    [property: JsonConverter(typeof(JsonStringEnumConverter<DayOfWeek>))] DayOfWeek Day,
    TimeOnly Start,
    TimeOnly End);

/// <summary>One session of one date, named by its times.</summary>
public sealed record SessionDateResponse(DateOnly Date, TimeOnly Start, TimeOnly End);
