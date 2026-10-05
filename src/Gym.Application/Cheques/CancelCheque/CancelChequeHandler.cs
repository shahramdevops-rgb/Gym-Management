using Gym.Application.Common;
using Gym.Domain.Cheques;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Cheques.CancelCheque;

/// <summary>
/// Takes a pending cheque out of the reminder without erasing it (BUSINESS_RULES.md §9
/// <i>Cheques</i>: never deleted). Owner only.
/// </summary>
public sealed class CancelChequeHandler(IAppDbContext db, TimeProvider time, ICurrentUser currentUser)
{
    public async Task<Result<ChequeResponse>> Handle(
        Guid id, CancelChequeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var cheque = await db.Cheques.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (cheque is null)
        {
            return Result.Failure<ChequeResponse>(ChequeErrors.NotFound);
        }

        // The endpoint's policy requires an authenticated user, so this is a wiring bug if hit.
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Cancelling a cheque was called without an authenticated user.");

        var cancelled = cheque.Cancel(command.Reason, time.GetUtcNow(), userId);
        if (cancelled.IsFailure)
        {
            return Result.Failure<ChequeResponse>(cancelled.Error);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ChequeResponse>(ChequeErrors.ChangedConcurrently);
        }

        return ChequeResponse.From(cheque);
    }
}
