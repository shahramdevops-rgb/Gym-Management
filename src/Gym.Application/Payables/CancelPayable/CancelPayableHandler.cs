using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Payables;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Payables.CancelPayable;

/// <summary>
/// Takes a pending cheque or instalment out of the reminder without erasing it (BUSINESS_RULES.md
/// §9 <i>Cheques and instalments</i>: never deleted). Owner only.
/// </summary>
public sealed class CancelPayableHandler(IAppDbContext db, TimeProvider time, ICurrentUser currentUser)
{
    public async Task<Result<PayableResponse>> Handle(
        Guid id, CancelPayableCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var payable = await db.Payables.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (payable is null)
        {
            return Result.Failure<PayableResponse>(PayableErrors.NotFound);
        }

        // The endpoint's policy requires an authenticated user, so this is a wiring bug if hit.
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Cancelling a cheque or instalment was called without an authenticated user.");

        var cancelled = payable.Cancel(command.Reason, time.GetUtcNow(), userId);
        if (cancelled.IsFailure)
        {
            return Result.Failure<PayableResponse>(cancelled.Error);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<PayableResponse>(PayableErrors.ChangedConcurrently);
        }

        return await PayableResponses.ForAsync(db, payable, cancellationToken);
    }
}
