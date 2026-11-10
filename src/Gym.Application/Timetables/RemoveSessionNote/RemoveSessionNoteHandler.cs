using Gym.Application.Bookings;
using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Timetables;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Timetables.RemoveSessionNote;

/// <summary>
/// The trainer removes the note on one date's session (BUSINESS_RULES.md §15 <i>Session notes</i>,
/// task 15.7e), until the session ends. A timetable is not a financial record: the row goes.
/// </summary>
/// <remarks>
/// A note whose session was removed or moved is out of sight; it can still be removed, though no
/// screen offers it. Under the trainer's row lock, as every change of the timetable.
/// </remarks>
public sealed class RemoveSessionNoteHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<SessionStudentsResponse>> Handle(
        Guid siteAccountId, DateOnly date, RemoveSessionNoteCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var trainer = await CoachingAccess.TrainerAsync(db, siteAccountId, cancellationToken);
        if (trainer.IsFailure)
        {
            return Result.Failure<SessionStudentsResponse>(trainer.Error);
        }

        var trainerId = trainer.Value.Id;
        await db.LockTrainerAsync(trainerId, cancellationToken);

        var note = await db.SessionNotes.SingleOrDefaultAsync(
            row => row.TrainerId == trainerId && row.Date == date && row.Start == command.Start && row.End == command.End,
            cancellationToken);
        if (note is null)
        {
            return Result.Failure<SessionStudentsResponse>(TimetableErrors.NoteNotFound);
        }

        var today = calendar.Today();
        var timeOfDay = calendar.TimeOfDay();
        var allowed = note.CheckChange(today, timeOfDay);
        if (allowed.IsFailure)
        {
            return Result.Failure<SessionStudentsResponse>(allowed.Error);
        }

        db.SessionNotes.Remove(note);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await SessionStudentsResponse.ForAsync(db, trainerId, date, today, timeOfDay, cancellationToken);
    }
}
