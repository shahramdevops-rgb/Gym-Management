using Gym.Application.Common;
using Gym.Application.Notifications.SendTrainerRequestSms;
using Gym.Domain.Coaching;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Coaching.RequestTrainer;

/// <summary>
/// A member picks a trainer on the site (BUSINESS_RULES.md §15 <i>A member and their trainer</i>):
/// a request the trainer accepts or declines. A request already waiting for another trainer is
/// replaced; the member's trainer now stays until the new one accepts.
/// </summary>
/// <remarks>
/// <b>Under the coaching lock</b>, so two presses at once make one request, and a trainer the Owner
/// is stopping at that moment is either still a trainer for the whole of this, or already not. The
/// trainer's SMS (task 15.4c) is asked for after the commit, and only for a new request.
/// </remarks>
public sealed class RequestTrainerHandler(IAppDbContext db, IGymCalendar calendar, TrainerRequestSms trainerRequestSms)
{
    public async Task<Result<MyTrainerResponse>> Handle(Guid siteAccountId, RequestTrainerCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockCoachingAsync(cancellationToken);

        var access = await CoachingAccess.MemberAsync(db, siteAccountId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<MyTrainerResponse>(access.Error);
        }

        var member = access.Value;

        var trainer = await db.Trainers.AsNoTracking().Include(row => row.Periods)
            .SingleOrDefaultAsync(row => row.Id == command.TrainerId, cancellationToken);
        if (trainer is not { IsActive: true })
        {
            return Result.Failure<MyTrainerResponse>(CoachingErrors.TrainerNotFound);
        }

        // One person, member and trainer with one phone (§15 Trainers).
        if (trainer.PhoneNumber == member.PhoneNumber)
        {
            return Result.Failure<MyTrainerResponse>(CoachingErrors.CannotPickYourself);
        }

        if (await db.TrainerLinks.AnyAsync(
                link => link.MemberId == member.Id && link.TrainerId == trainer.Id && link.EndedOn == null,
                cancellationToken))
        {
            return Result.Failure<MyTrainerResponse>(CoachingErrors.AlreadyYourTrainer);
        }

        var waiting = await db.TrainerRequests.SingleOrDefaultAsync(
            request => request.MemberId == member.Id && request.Status == TrainerRequestStatus.Pending,
            cancellationToken);

        // A second press, or the app sent again: the request is already there, and counts once.
        if (waiting?.TrainerId == trainer.Id)
        {
            return await MyTrainerResponse.ForAsync(db, member.Id, cancellationToken);
        }

        var today = calendar.Today();
        var madeToday = await db.TrainerRequests.CountAsync(
            request => request.MemberId == member.Id && request.RequestedOn == today,
            cancellationToken);

        var created = TrainerRequest.Create(member.Id, trainer.Id, today, madeToday);
        if (created.IsFailure)
        {
            return Result.Failure<MyTrainerResponse>(created.Error);
        }

        // Saved before the new one: the index on waiting requests holds one per member at every write.
        if (waiting is not null)
        {
            waiting.Replace();
            await db.SaveChangesAsync(cancellationToken);
        }

        db.TrainerRequests.Add(created.Value);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await trainerRequestSms.QueueAsync(created.Value.Id, CancellationToken.None);

        return await MyTrainerResponse.ForAsync(db, member.Id, cancellationToken);
    }
}
