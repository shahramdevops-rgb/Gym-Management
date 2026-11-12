using Gym.Application.Bookings;
using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Domain.Bookings;
using Gym.Domain.Common;
using Gym.Domain.Timetables;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Timetables.SetSessionNote;

/// <summary>
/// The trainer writes or changes the note on one date's session (BUSINESS_RULES.md §15 <i>Session
/// notes</i>, task 15.7e): a session the date has, until it ends. The timetable itself does not
/// change, so the date does not become "changed by hand", and no student is told.
/// </summary>
/// <remarks>Under the trainer's row lock, as every change of the timetable.</remarks>
public sealed class SetSessionNoteHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<SessionStudentsResponse>> Handle(
        Guid siteAccountId, DateOnly date, SetSessionNoteCommand command, CancellationToken cancellationToken)
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

        // Times no session can have are the request's mistake, not an unexpected failure.
        if (command.End <= command.Start)
        {
            return Result.Failure<SessionStudentsResponse>(BookingErrors.NoSuchSession);
        }

        var today = calendar.Today();
        var timeOfDay = calendar.TimeOfDay();

        var note = await db.SessionNotes.SingleOrDefaultAsync(
            row => row.TrainerId == trainerId && row.Date == date && row.Start == command.Start && row.End == command.End,
            cancellationToken);

        Result written;
        if (note is null)
        {
            var created = SessionNote.Write(trainerId, date, command.Start, command.End, command.Text, today, timeOfDay);
            if (created.IsSuccess)
            {
                db.SessionNotes.Add(created.Value);
            }

            written = created;
        }
        else
        {
            written = note.Rewrite(command.Text, today, timeOfDay);
        }

        if (written.IsFailure)
        {
            return Result.Failure<SessionStudentsResponse>(written.Error);
        }

        // Only on a session the date has: read after the Domain's checks, so a date out of range or
        // over says so rather than "no such session".
        var timetable = await TrainerTimetable.DatesAsync(db, trainerId, date, date, cancellationToken);
        if (!timetable.SessionsOn(date).Any(session => session.Start == command.Start && session.End == command.End))
        {
            return Result.Failure<SessionStudentsResponse>(BookingErrors.NoSuchSession);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await SessionStudentsResponse.ForAsync(db, trainerId, date, today, timeOfDay, cancellationToken);
    }
}
