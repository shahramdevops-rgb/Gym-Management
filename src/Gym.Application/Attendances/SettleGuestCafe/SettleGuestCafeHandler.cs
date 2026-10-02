using Gym.Application.Common;
using Gym.Application.Payments.SettleMemberDebt;
using Gym.Domain.Attendances;
using Gym.Domain.Common;
using Gym.Domain.Payments;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Attendances.SettleGuestCafe;

/// <summary>
/// «تسویه یکجا» for a guest: pays every unpaid cafe order of the visit in full, one ordinary payment
/// per order, in one transaction (BUSINESS_RULES.md §7 <i>Guest visit</i>), so the guest can then
/// check out. Front desk work, so both roles.
/// </summary>
/// <remarks>
/// <para>
/// The guest's twin of <c>SettleMemberDebtHandler</c>, simpler because there is nothing to choose:
/// a guest leaves no debt behind, so everything is paid and nothing is spread. Each payment is
/// exactly what the order's own payment form would have written, so refunds and cancellations work
/// on it unchanged.
/// </para>
/// <para>
/// The orders are read again under the visit's lock, which a guest's order and its payments also
/// take, and the amount must match that fresh total. Allowed after the visit closed too: the
/// nightly job closes a guest's visit with its orders unpaid (§7 <i>Auto-checkout</i>).
/// </para>
/// </remarks>
public sealed class SettleGuestCafeHandler(IAppDbContext db, TimeProvider time, ICurrentUser currentUser)
{
    public async Task<Result<SettlementResponse>> Handle(
        Guid attendanceId, SettleGuestCafeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var visit = await db.Attendances.AsNoTracking()
            .Where(a => a.Id == attendanceId)
            .Select(a => new { a.MemberId })
            .SingleOrDefaultAsync(cancellationToken);
        if (visit is null)
        {
            return Result.Failure<SettlementResponse>(AttendanceErrors.NotFound);
        }

        // A member's purchases go on their account and are settled with the rest of their debt.
        if (visit.MemberId is not null)
        {
            return Result.Failure<SettlementResponse>(AttendanceErrors.NotGuestVisit);
        }

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockAttendanceAsync(attendanceId, cancellationToken);

        var unpaid = await GuestCafe.UnpaidOrdersAsync(db, attendanceId, cancellationToken);
        var owed = unpaid.Sum(order => order.Outstanding);

        // Nothing owed, or a figure other than the one the desk was shown: an order was paid,
        // cancelled or added since the box opened. The desk has to look again.
        if (owed == 0 || owed != command.Amount)
        {
            return Result.Failure<SettlementResponse>(SettlementErrors.DebtChanged);
        }

        // The endpoint's policy requires an authenticated user, so this is a wiring bug if hit.
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Settle guest cafe was called without an authenticated user.");

        // One moment for every row, so the history shows them as the one handover they were.
        var paidAt = time.GetUtcNow();
        var payments = new List<SettlementPaymentResponse>();
        foreach (var order in unpaid)
        {
            var registered = Payment.RegisterForCafeOrder(
                order.Order.Id, order.Outstanding, command.Method, command.ReferenceNumber, userId, paidAt);
            if (registered.IsFailure)
            {
                return Result.Failure<SettlementResponse>(registered.Error);
            }

            db.Payments.Add(registered.Value);
            payments.Add(new SettlementPaymentResponse(
                registered.Value.Id, PaymentTargetKind.CafeOrder, order.Order.Id, order.Outstanding, 0m));
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SettlementResponse(command.Amount, command.Method, payments, RemainingDebt: 0m);
    }
}
