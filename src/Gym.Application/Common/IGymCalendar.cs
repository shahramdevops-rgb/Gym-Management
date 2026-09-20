namespace Gym.Application.Common;

/// <summary>
/// "Today" as the gym sees it: the current date in <c>Gym:TimeZone</c> (BUSINESS_RULES.md §0).
/// </summary>
/// <remarks>
/// The one place that turns the clock into a business date. Every rule that depends on the day
/// (is this subscription expired, where does a new one start) takes the result as a
/// <see cref="DateOnly"/>, so they all agree on when midnight is.
/// </remarks>
public interface IGymCalendar
{
    DateOnly Today();

    /// <summary>
    /// Midnight of <paramref name="date"/> in <c>Gym:TimeZone</c>, as a UTC moment. Turns a
    /// business date range into a moment range, for filtering a <c>timestamptz</c> column such as
    /// <c>Attendance.CheckedInAt</c> by day.
    /// </summary>
    DateTimeOffset StartOfDayUtc(DateOnly date);
}
