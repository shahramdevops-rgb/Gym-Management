using Gym.Application.Common;
using Gym.Application.Payments.SettleMemberDebt;
using Gym.Domain.Attendances;
using Gym.Domain.Common;
using Gym.Domain.Payments;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Attendances.SettleGuestVisit;

/// <summary>
/// «تسویه یکجا» for a guest: pays everything the visit still owes in full, its cafe orders and then
/// its هوازی and sales, one ordinary payment per item, in one transaction (BUSINESS_RULES.md §7
/// <i>Guest visit</i>), so the guest can then check out. Front desk work, so both roles.
/// </summary>
/// <remarks>
/// <para>
/// The guest's twin of <c>SettleMemberDebtHandler</c>, simpler because there is nothing to choose:
/// a guest leaves no debt behind, so everything is paid and nothing is spread. Each payment is
/// exactly what the item's own payment form would have written, so refunds, cancellations and
/// voids work on it unchanged.
/// </para>
/// <para>
/// The purchases are read again under the visit's lock, which a guest's order, charge and their
/// payments also take, and the amount must match that fresh total. Allowed after the visit closed
/// too: the nightly job closes a guest's visit with its purchases unpaid (§7 <i>Auto-checkout</i>).
/// </para>
/// </remarks>
public sealed class SettleGuestVisitHandler(IAppDbContext db, TimeProvider time, ICurrentUser currentUser)
{
    public async Task<Result<SettlementResponse>> Handle(
        Guid attendanceId, SettleGuestVisitCommand command, CancellationToken cancellationToken)
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

        var unpaid = await GuestPurchases.UnpaidAsync(db, attendanceId, cancellationToken);

        // Nothing owed, or a figure other than the one the desk was shown: a purchase was paid,
        // voided, cancelled or added since the box opened. The desk has to look again.
        if (!unpaid.Any || unpaid.Total != command.Amount)
        {
            return Result.Failure<SettlementResponse>(SettlementErrors.DebtChanged);
        }

        // The endpoint's policy requires an authenticated user, so this is a wiring bug if hit.
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Settle guest visit was called without an authenticated user.");

        // One moment and one settlement id for every row, so the history shows them as the one
        // handover they were.
        var paidAt = time.GetUtcNow();
        var settlementId = Guid.CreateVersion7();
        var payments = new List<SettlementPaymentResponse>();

        // The cafe first, then the هوازی and the sales, the order a member's settlement uses (§5).
        foreach (var order in unpaid.Orders)
        {
            var registered = Payment.RegisterForCafeOrder(
                order.Order.Id, order.Outstanding, command.Method, command.ReferenceNumber, userId, paidAt);
            if (registered.IsFailure)
            {
                return Result.Failure<SettlementResponse>(registered.Error);
            }

            Add(registered.Value, PaymentTargetKind.CafeOrder, order.Order.Id, order.Outstanding);
        }

        foreach (var charge in unpaid.Charges)
        {
            var registered = Payment.RegisterForServiceCharge(
                charge.Charge.Id, charge.Outstanding, command.Method, command.ReferenceNumber, userId, paidAt);
            if (registered.IsFailure)
            {
                return Result.Failure<SettlementResponse>(registered.Error);
            }

            Add(registered.Value, PaymentTargetKind.ServiceCharge, charge.Charge.Id, charge.Outstanding);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SettlementResponse(command.Amount, command.Method, payments, RemainingDebt: 0m);

        void Add(Payment payment, PaymentTargetKind kind, Guid targetId, decimal amount)
        {
            payment.JoinSettlement(settlementId);
            db.Payments.Add(payment);
            payments.Add(new SettlementPaymentResponse(payment.Id, kind, targetId, amount, 0m));
        }
    }
}
