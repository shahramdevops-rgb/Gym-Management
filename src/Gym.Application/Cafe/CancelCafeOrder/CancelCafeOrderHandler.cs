using Gym.Application.Common;
using Gym.Domain.Cafe;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Cafe.CancelCafeOrder;

/// <summary>
/// Undoes a sale without erasing it (BUSINESS_RULES.md §8, §5: financial records are never
/// deleted). The order stops counting toward the member's debt, and anything already paid on it
/// is refunded in the same transaction.
/// </summary>
/// <remarks>
/// <para>
/// Staff or Owner, decided by the Owner 1405/07/03: the customer is still standing at the desk and
/// the person who rang it up has to be able to take it back. The same exception §7 makes for
/// voiding a service charge; the controls are the required reason and the audit log.
/// </para>
/// <para>
/// <b>Money goes back the way it came</b>: one refund per payment method still in credit, the same
/// as a voided service charge, so the screen never has to ask which method to use. Nothing is put
/// back on a shelf, because the gym counts no stock.
/// </para>
/// </remarks>
public sealed class CancelCafeOrderHandler(IAppDbContext db, TimeProvider time, ICurrentUser currentUser)
{
    public async Task<Result<CafeOrderResponse>> Handle(
        Guid id, CancelCafeOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var order = await db.CafeOrders
            .Include(o => o.Items)
            .SingleOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (order is null)
        {
            return Result.Failure<CafeOrderResponse>(CafeOrderErrors.NotFound);
        }

        // The refunds are written from what the payments add up to, so that sum is read under the
        // same per-member lock a payment takes — otherwise an instalment arriving at this moment
        // would stay in the gym's books against an order that owes nothing. A walk-in order has no
        // member to lock, but it also takes no payments after it was created; two cancels racing
        // for it are stopped by the order's xmin, which rolls back the loser's refunds with it.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (order.MemberId is { } memberId)
        {
            await db.LockMemberAsync(memberId, cancellationToken);
        }

        // The endpoint's policy requires an authenticated user, so this is a wiring bug if hit.
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Cancelling a cafe order was called without an authenticated user.");

        var now = time.GetUtcNow();

        var cancelled = order.Cancel(command.Reason, now, userId);
        if (cancelled.IsFailure)
        {
            return Result.Failure<CafeOrderResponse>(cancelled.Error);
        }

        await CafeOrderRefunder.RefundNetPaidAsync(db, order, userId, now, cancellationToken);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<CafeOrderResponse>(CafeOrderErrors.ChangedConcurrently);
        }

        var memberFullName = order.MemberId is { } orderMemberId
            ? await db.Members.AsNoTracking()
                .Where(member => member.Id == orderMemberId)
                .Select(member => member.FullName)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        // Net paid is zero by construction: whatever had been collected was just refunded.
        return CafeOrderResponse.From(order, memberFullName, netPaid: 0);
    }
}
