using Gym.Domain.Bookings;
using Gym.Domain.Common;

namespace Gym.Domain.Timetables;

/// <summary>
/// The trainer's note on one date's session, «یادداشت این روز» (BUSINESS_RULES.md §15 <i>Session
/// notes</i>, task 15.7e), e.g. «فردا تمرین پا است». Every student of that session sees it, those on
/// leave too, under the date in «سانس‌های من»; only the trainer writes it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its own row, not part of the day.</b> A note written in the day's <see cref="TimetableDay"/>
/// would make that date "changed by hand", and a later pattern would no longer reach it. So a date's
/// note names its session by its times, as a booking does, and the timetable stays as it was.
/// </para>
/// <para>
/// <b>Shown only while the session is there.</b> A session the trainer removes or moves the times of
/// takes its note out of sight; the row stays, and the note shows again if the session comes back.
/// </para>
/// <para>
/// <b>Changed until the session ends</b>, then kept as it was, for the trainer's earlier days. A
/// timetable is not a financial record, so removing one deletes the row. One per trainer, date and
/// session, held by a unique index too.
/// </para>
/// </remarks>
public sealed class SessionNote : Entity
{
    // For EF Core.
    private SessionNote()
    {
    }

    public Guid TrainerId { get; private set; }

    public DateOnly Date { get; private set; }

    public TimeOnly Start { get; private set; }

    public TimeOnly End { get; private set; }

    public string Text { get; private set; } = string.Empty;

    /// <summary>Postgres <c>xmin</c>: the second of two changes at once fails instead of overwriting.</summary>
    public uint Version { get; private set; }

    /// <summary>
    /// The trainer's first note on the session of <paramref name="date"/> with these times. Whether
    /// that session is in the trainer's timetable that date is read by the caller.
    /// </summary>
    /// <param name="today">The gym's today.</param>
    /// <param name="timeOfDay">The time of day now, in the gym's time zone.</param>
    public static Result<SessionNote> Write(
        Guid trainerId,
        DateOnly date,
        TimeOnly start,
        TimeOnly end,
        string? text,
        DateOnly today,
        TimeOnly timeOfDay)
    {
        CheckId(trainerId, nameof(trainerId));
        if (end <= start)
        {
            throw new ArgumentException("A session ends after it starts.", nameof(end));
        }

        if (date > TimetableRules.LastDay(today))
        {
            return Result.Failure<SessionNote>(TimetableErrors.DayOutOfRange);
        }

        var note = new SessionNote { TrainerId = trainerId, Date = date, Start = start, End = end };
        var written = note.Rewrite(text, today, timeOfDay);

        return written.IsFailure ? Result.Failure<SessionNote>(written.Error) : note;
    }

    /// <summary>The trainer changes the note's text, until the session ends.</summary>
    public Result Rewrite(string? text, DateOnly today, TimeOnly timeOfDay)
    {
        var allowed = CheckChange(today, timeOfDay);
        if (allowed.IsFailure)
        {
            return allowed;
        }

        var kept = SessionNoteText.Normalize(text);
        if (kept is null)
        {
            return Result.Failure(TimetableErrors.NoteEmpty);
        }

        if (kept.Length > SessionNoteText.MaxDateLength)
        {
            return Result.Failure(TimetableErrors.DateNoteTooLong);
        }

        Text = kept;

        return Result.Success();
    }

    /// <summary>Whether the note may change or be removed now: until the session ends.</summary>
    public Result CheckChange(DateOnly today, TimeOnly timeOfDay) =>
        BookingRules.HasEnded(Date, End, today, timeOfDay)
            ? Result.Failure(TimetableErrors.NoteSessionEnded)
            : Result.Success();

    private static void CheckId(Guid id, string name)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The id is empty.", name);
        }
    }
}
