namespace Gym.Domain.Timetables;

/// <summary>
/// One session of a day as the trainer plans it (BUSINESS_RULES.md §15 <i>The trainer's
/// sessions</i>): from <see cref="Start"/> to <see cref="End"/> on that day, for up to
/// <see cref="Capacity"/> students; <see cref="IsVip"/> when the trainer marks it VIP (BUSINESS_RULES.md
/// §15 <i>VIP sessions</i>, 15.6e), for now only a label.
/// </summary>
/// <remarks>
/// A value, compared by all its parts: two sessions of one day are the same session when they
/// start and end together, and a started session "stays as it was" when all are equal, its label too.
/// </remarks>
public sealed record PlannedSession(TimeOnly Start, TimeOnly End, int Capacity, bool IsVip = false);

/// <summary>A session of the weekly pattern: a <see cref="PlannedSession"/> on every <see cref="Day"/>.</summary>
public sealed record WeeklySession(DayOfWeek Day, TimeOnly Start, TimeOnly End, int Capacity, bool IsVip = false)
{
    public PlannedSession ToPlanned() => new(Start, End, Capacity, IsVip);
}
