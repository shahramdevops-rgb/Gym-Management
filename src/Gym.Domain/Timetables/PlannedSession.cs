namespace Gym.Domain.Timetables;

/// <summary>
/// One session of a day as the trainer plans it (BUSINESS_RULES.md §15 <i>The trainer's
/// sessions</i>): from <see cref="Start"/> to <see cref="End"/> on that day, for up to
/// <see cref="Capacity"/> students; <see cref="IsVip"/> when the trainer marks it VIP (BUSINESS_RULES.md
/// §15 <i>VIP sessions</i>, 15.6e), for now only a label; <see cref="Note"/> the trainer's standing
/// note on it («یادداشت ثابت», §15 <i>Session notes</i>, 15.7e), seen every week it is there.
/// </summary>
/// <remarks>
/// A value, compared by all its parts: two sessions of one day are the same session when they
/// start and end together, and a started session "stays as it was" when all are equal, its label and
/// note too. The note is kept trimmed, and a blank one is none, so two equal notes compare equal.
/// </remarks>
public sealed record PlannedSession(TimeOnly Start, TimeOnly End, int Capacity, bool IsVip = false, string? Note = null)
{
    public string? Note { get; } = SessionNoteText.Normalize(Note);
}

/// <summary>A session of the weekly pattern: a <see cref="PlannedSession"/> on every <see cref="Day"/>.</summary>
public sealed record WeeklySession(DayOfWeek Day, TimeOnly Start, TimeOnly End, int Capacity, bool IsVip = false, string? Note = null)
{
    public string? Note { get; } = SessionNoteText.Normalize(Note);

    public PlannedSession ToPlanned() => new(Start, End, Capacity, IsVip, Note);
}
