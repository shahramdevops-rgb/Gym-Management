namespace Gym.Application.Attendances.TodayByHour;

/// <summary>
/// Today's check-ins hour by hour, beside what the same weekday usually looks like
/// (BUSINESS_RULES.md §6 <i>Today by hour</i>). Counts only, never money.
/// </summary>
/// <param name="Date">Today in the gym's time zone, the day the counts are for.</param>
/// <param name="DaysAveraged">
/// How many of the previous 4 same weekdays had at least one check-in and so went into the
/// averages: 0 to 4. With 0 there is nothing to compare with yet, and every average is 0.
/// </param>
/// <param name="Hours">Always 24 rows, hour 0 to 23 in the gym's time zone, in order.</param>
public sealed record TodayByHourResponse(
    DateOnly Date,
    int DaysAveraged,
    IReadOnlyList<HourCountResponse> Hours);

/// <param name="Hour">0 to 23, in the gym's time zone.</param>
/// <param name="Today">Check-ins today whose moment falls in this hour, cancelled ones left out.</param>
/// <param name="Average">
/// The same hour's mean over the days counted in <see cref="TodayByHourResponse.DaysAveraged"/>,
/// rounded to one decimal place.
/// </param>
public sealed record HourCountResponse(int Hour, int Today, double Average);
