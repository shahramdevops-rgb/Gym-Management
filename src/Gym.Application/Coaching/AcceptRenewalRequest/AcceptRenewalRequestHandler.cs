using Gym.Application.Common;
using Gym.Domain.Coaching;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Coaching.AcceptRenewalRequest;

/// <summary>
/// «تمدید» on a student's «درخواست تمدید» in «درخواست‌ها» (BUSINESS_RULES.md §15 <i>The coaching
/// period</i>, task 15.7o): the trainer typed the days and said yes, and the coaching is renewed as by
/// «تمدید» on «برنامهٔ ثابت»: the new period starts the day after the last day, or today once it is over.
/// </summary>
/// <remarks>
/// Under the coaching lock, then the trainer's, in the order «تمدید» takes them: the student taking it
/// back or the coaching ending at the same moment comes either before, and this finds it no longer
/// waiting, or after; «ذخیره برنامه» reads the period before or after this, never half of it.
/// </remarks>
public sealed class AcceptRenewalRequestHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<StudentsResponse>> Handle(
        Guid siteAccountId, Guid requestId, AcceptRenewalRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockCoachingAsync(cancellationToken);

        var trainer = await CoachingAccess.TrainerAsync(db, siteAccountId, cancellationToken);
        if (trainer.IsFailure)
        {
            return Result.Failure<StudentsResponse>(trainer.Error);
        }

        await db.LockTrainerAsync(trainer.Value.Id, cancellationToken);

        var request = await RenewalRequests.OfTrainerAsync(db, trainer.Value.Id, requestId, cancellationToken);
        if (request is null)
        {
            return Result.Failure<StudentsResponse>(CoachingErrors.RenewalRequestNotFound);
        }

        // Checked before renewing: a request taken back or closed leaves the period as it is.
        if (!request.IsPending)
        {
            return Result.Failure<StudentsResponse>(CoachingErrors.RenewalNoLongerPending);
        }

        var link = await db.TrainerLinks
            .Include(row => row.Periods)
            .SingleAsync(row => row.Id == request.LinkId, cancellationToken);

        var today = calendar.Today();
        var period = link.Renew(command.Days, today);
        if (period.IsFailure)
        {
            return Result.Failure<StudentsResponse>(period.Error);
        }

        request.Renew(today);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await StudentsResponse.ForAsync(db, trainer.Value.Id, cancellationToken);
    }
}
