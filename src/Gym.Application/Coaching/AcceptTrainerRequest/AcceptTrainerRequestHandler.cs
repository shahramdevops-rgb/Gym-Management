using Gym.Application.Common;
using Gym.Domain.Coaching;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Coaching.AcceptTrainerRequest;

/// <summary>
/// The trainer accepts a member's request (BUSINESS_RULES.md §15 <i>A member and their trainer</i>):
/// the member becomes their student today, and the member's link to an earlier trainer ends today,
/// so that trainer stops seeing them today.
/// </summary>
/// <remarks>
/// Under the coaching lock: the member taking the request back, or picking someone else, at the same
/// moment comes either before, and this finds the request no longer waiting, or after.
/// </remarks>
public sealed class AcceptTrainerRequestHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<StudentsResponse>> Handle(Guid siteAccountId, Guid requestId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockCoachingAsync(cancellationToken);

        var trainer = await CoachingAccess.TrainerAsync(db, siteAccountId, cancellationToken);
        if (trainer.IsFailure)
        {
            return Result.Failure<StudentsResponse>(trainer.Error);
        }

        var request = await db.TrainerRequests.SingleOrDefaultAsync(
            row => row.Id == requestId && row.TrainerId == trainer.Value.Id,
            cancellationToken);
        if (request is null)
        {
            return Result.Failure<StudentsResponse>(CoachingErrors.RequestNotFound);
        }

        // Deactivated at the desk since asking: the trainer's list leaves the request out, and it
        // waits there until the member is active again.
        var memberIsActive = await db.Members.AsNoTracking()
            .Where(member => member.Id == request.MemberId)
            .Select(member => member.IsActive)
            .SingleAsync(cancellationToken);
        if (!memberIsActive)
        {
            return Result.Failure<StudentsResponse>(CoachingErrors.RequestNotFound);
        }

        var today = calendar.Today();
        var link = request.Accept(today);
        if (link.IsFailure)
        {
            return Result.Failure<StudentsResponse>(link.Error);
        }

        // The earlier trainer's link ends first: the index holds one open link per member at every write.
        var earlier = await db.TrainerLinks.SingleOrDefaultAsync(
            row => row.MemberId == request.MemberId && row.EndedOn == null,
            cancellationToken);
        if (earlier is not null)
        {
            earlier.End(today, TrainerLinkEndReason.NewTrainer);
            await db.SaveChangesAsync(cancellationToken);
        }

        db.TrainerLinks.Add(link.Value);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await StudentsResponse.ForAsync(db, trainer.Value.Id, cancellationToken);
    }
}
