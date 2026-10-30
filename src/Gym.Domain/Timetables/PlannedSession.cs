namespace Gym.Domain.Timetables;

/// <summary>
/// One session of a day as the trainer plans it (BUSINESS_RULES.md §15 <i>The trainer's
/// sessions</i>): from <see cref="Start"/> to <see cref="End"/> on that day, for up to
/// <see cref="Capacity"/> students.
/// </summary>
/// <remarks>
/// A value, compared by its three parts: two sessions of one day are the same session when they
/// start and end together, and a started session "stays as it was" when all three are equal.
/// </remarks>
public sealed record PlannedSession(TimeOnly Start, TimeOnly End, int Capacity);

/// <summary>A session of the weekly pattern: a <see cref="PlannedSession"/> on every <see cref="Day"/>.</summary>
public sealed record WeeklySession(DayOfWeek Day, TimeOnly Start, TimeOnly End, int Capacity)
{
    public PlannedSession ToPlanned() => new(Start, End, Capacity);
}
