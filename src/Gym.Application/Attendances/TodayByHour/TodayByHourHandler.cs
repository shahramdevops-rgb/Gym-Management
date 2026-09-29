using Gym.Application.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Attendances.TodayByHour;

/// <summary>
/// The chart under the locker map: today's check-ins per hour and the same weekday's average over
/// the previous 4 weeks (BUSINESS_RULES.md §6 <i>Today by hour</i>, roadmap 6.5.14).
/// </summary>
/// <remarks>
/// Five days are read: today and the same weekday 1 to 4 weeks back. Each day is its own range of
/// <c>checked_in_at</c> between two of the gym's midnights (served by the index on that column), and
/// only the moments come back: a few hundred at most. They are put into hours here, in C#, by
/// converting each moment to the gym's time zone, which is easier to read than doing the same with
/// <c>AT TIME ZONE</c> in SQL and just as right on a day the clocks change.
/// <para>
/// A past day with no counted check-in is left out of the average rather than counted as zero: the
/// gym was closed, or the system was not in use yet (the developer's decision, 1405/07/07).
/// </para>
/// </remarks>
public sealed class TodayByHourHandler(IAppDbContext db, IGymCalendar calendar)
{
    /// <summary>How many weeks back the average reaches.</summary>
    public const int WeeksAveraged = 4;

    private const int HoursInDay = 24;

    public async Task<TodayByHourResponse> Handle(CancellationToken cancellationToken)
    {
        var today = calendar.Today();
        var todayCounts = await CountByHourAsync(today, cancellationToken);

        var totals = new int[HoursInDay];
        var daysAveraged = 0;
        for (var week = 1; week <= WeeksAveraged; week++)
        {
            var counts = await CountByHourAsync(today.AddDays(-7 * week), cancellationToken);
            if (counts.Sum() == 0)
            {
                continue;
            }

            daysAveraged++;
            for (var hour = 0; hour < HoursInDay; hour++)
            {
                totals[hour] += counts[hour];
            }
        }

        var hours = Enumerable.Range(0, HoursInDay)
            .Select(hour => new HourCountResponse(
                hour,
                todayCounts[hour],
                daysAveraged == 0 ? 0 : Math.Round((double)totals[hour] / daysAveraged, 1, MidpointRounding.AwayFromZero)))
            .ToList();

        return new TodayByHourResponse(today, daysAveraged, hours);
    }

    /// <summary>The day's check-ins, cancelled ones left out, counted by their hour in the gym's zone.</summary>
    private async Task<int[]> CountByHourAsync(DateOnly day, CancellationToken cancellationToken)
    {
        var start = calendar.StartOfDayUtc(day);
        var end = calendar.StartOfDayUtc(day.AddDays(1));

        var moments = await db.Attendances
            .AsNoTracking()
            .Where(a => a.CheckedInAt >= start && a.CheckedInAt < end && a.CancelledAt == null)
            .Select(a => a.CheckedInAt)
            .ToListAsync(cancellationToken);

        var counts = new int[HoursInDay];
        foreach (var moment in moments)
        {
            counts[TimeZoneInfo.ConvertTime(moment, calendar.TimeZone).Hour]++;
        }

        return counts;
    }
}
