using System.Text.Json.Serialization;

using Gym.Application.Timetables;
using Gym.Domain.Bookings;
using Gym.Domain.Timetables;

namespace Gym.Application.Bookings;

/// <summary>A date a student is in one of the trainer's sessions (BUSINESS_RULES.md §15 <i>Students in sessions</i>).</summary>
/// <param name="Moved">A date moved to, not one of the fixed schedule's.</param>
/// <param name="IsVip">The session that date is marked VIP (15.6e); never once it is removed.</param>
/// <param name="Removed">The trainer has since removed the session or moved its times: «لغو شد».</param>
/// <param name="Started">It has started: it can no longer be left or moved.</param>
/// <param name="Left">
/// The member is out of it by their own «نمی‌آیم», and may take it back («می‌آیم»): only in the
/// member's «سانس‌های من», where such a date is still listed until the session starts.
/// </param>
public sealed record PlaceResponse(DateOnly Date, TimeOnly Start, TimeOnly End, bool IsVip, bool Moved, bool Removed, bool Started, bool Left)
{
    internal static PlaceResponse From(BookedPlace place, TimetableDates timetable, DateOnly today, TimeOnly timeOfDay)
    {
        var session = BookingRules.SessionOf(place, timetable.SessionsOn(place.Date));

        return new(
            place.Date,
            place.Start,
            place.End,
            session?.IsVip ?? false,
            place.IsMoved,
            Removed: session is null,
            BookingRules.HasStarted(place.Date, place.Start, today, timeOfDay),
            Left: false);
    }

    /// <summary>A date the member left, its <paramref name="session"/> still there and not started.</summary>
    internal static PlaceResponse LeftBy(SessionSkip skip, PlannedSession session) =>
        new(skip.Date, skip.Start, skip.End, session.IsVip, Moved: false, Removed: false, Started: false, Left: true);
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
