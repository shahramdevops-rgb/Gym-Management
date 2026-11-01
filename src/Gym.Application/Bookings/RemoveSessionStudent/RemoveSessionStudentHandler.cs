using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Domain.Bookings;
using Gym.Domain.Common;

namespace Gym.Application.Bookings.RemoveSessionStudent;

/// <summary>
/// The trainer takes a student out of one date's session, as the student may (BUSINESS_RULES.md §15
/// <i>Students in sessions</i>): until the session starts. The other weeks stay as they are.
/// </summary>
/// <remarks>
/// A student whose link has ended can still be taken out of today's session, the last day the
/// link reaches. Under the trainer's row lock, as every change of the places.
/// </remarks>
public sealed class RemoveSessionStudentHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<SessionStudentsResponse>> Handle(
        Guid siteAccountId, DateOnly date, RemoveSessionStudentCommand command, CancellationToken cancellationToken)
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

        var place = await StudentSessions.PlaceAsync(db, trainerId, command.MemberId, date, command.Start, command.End, cancellationToken);
        if (place.IsFailure)
        {
            return Result.Failure<SessionStudentsResponse>(place.Error);
        }

        var today = calendar.Today();
        var timeOfDay = calendar.TimeOfDay();
        var takenOut = await StudentSessions.TakeOutAsync(db, place.Value, SkipReason.TakenOut, today, timeOfDay, cancellationToken);
        if (takenOut.IsFailure)
        {
            return Result.Failure<SessionStudentsResponse>(takenOut.Error);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await SessionStudentsResponse.ForAsync(db, trainerId, date, today, timeOfDay, cancellationToken);
    }
}
