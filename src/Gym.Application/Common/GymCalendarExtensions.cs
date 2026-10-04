namespace Gym.Application.Common;

/// <summary>Turning a moment back into the gym's day, for the reports that count by day.</summary>
public static class GymCalendarExtensions
{
    /// <summary>
    /// The day <paramref name="moment"/> falls on in <c>Gym:TimeZone</c>: 00:10 in Tehran is that
    /// day, even though it is still the evening before in UTC.
    /// </summary>
    public static DateOnly DayOf(this IGymCalendar calendar, DateTimeOffset moment)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(moment, calendar.TimeZone).DateTime);
    }
}
