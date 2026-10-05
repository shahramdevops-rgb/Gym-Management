using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Payables;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Payables.RevertPayable;

/// <summary>
/// «برگشت به در انتظار»: a payment marked by mistake goes back to pending, and its expense is voided
/// with the same reason, never deleted (BUSINESS_RULES.md §9 <i>Cheques and instalments</i>).
/// Owner only.
/// </summary>
/// <remarks>
/// Both rows change in one <c>SaveChanges</c>, so one transaction. <c>xmin</c> on both settles a
/// revert racing another revert, an edit or a second mark.
/// </remarks>
public sealed class RevertPayableHandler(IAppDbContext db, TimeProvider time, ICurrentUser currentUser)
{
    public async Task<Result<PayableResponse>> Handle(
        Guid id, RevertPayableCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var payable = await db.Payables.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (payable is null)
        {
            return Result.Failure<PayableResponse>(PayableErrors.NotFound);
        }

        if (!payable.IsPaid)
        {
            return Result.Failure<PayableResponse>(
                payable.IsCancelled ? PayableErrors.AlreadyCancelled : PayableErrors.NotPaid);
        }

        // A paid record always has its standing expense: they were saved together.
        var expense = await db.Expenses.SingleAsync(
            e => e.PayableId == payable.Id && e.VoidedAt == null, cancellationToken);

        // The endpoint's policy requires an authenticated user, so this is a wiring bug if hit.
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Reverting a payment was called without an authenticated user.");

        var reverted = payable.RevertToPending(expense, command.Reason, time.GetUtcNow(), userId);
        if (reverted.IsFailure)
        {
            return Result.Failure<PayableResponse>(reverted.Error);
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
