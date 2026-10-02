using Gym.Application.Common;
using Gym.Application.Members;
using Gym.Domain.Common;
using Gym.Domain.Members;
using Gym.Domain.Payments;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Payments.SettleMemberDebt;

/// <summary>
/// One amount, one method, several owed items: the system writes one ordinary payment per item
/// (BUSINESS_RULES.md §5 <i>Settling several items at once</i>). Owner and Staff.
/// </summary>
/// <remarks>
/// <para>
/// Nothing new is stored. Each row is exactly what the item's own payment form would have written,
/// so debt, payment status, refunds, voids and cancellations work on it unchanged.
/// </para>
/// <para>
/// The debt is read again under the member lock, which every handler that pays, voids or cancels
/// one of these items also takes. What the desk ticked is compared with that fresh reading, so a
/// charge voided or an order paid while the form was open is caught here rather than overpaid.
/// </para>
/// </remarks>
public sealed class SettleMemberDebtHandler(IAppDbContext db, TimeProvider time, ICurrentUser currentUser)
{
    public async Task<Result<SettlementResponse>> Handle(
        Guid memberId, SettleMemberDebtCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var exists = await db.Members.AsNoTracking().AnyAsync(member => member.Id == memberId, cancellationToken);
        if (!exists)
        {
            return Result.Failure<SettlementResponse>(MemberErrors.NotFound);
        }

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockMemberAsync(memberId, cancellationToken);

        var debt = await MemberDebt.GetItemsAsync(db, memberId, cancellationToken);
        var owed = debt.ToDictionary(item => (item.Kind, item.Id));

        // An item missing from the debt is paid in full, voided, cancelled, or someone else's; one
        // with a different figure changed while the desk was looking. Either way the desk has to
        // look again before any money is spread.
        var items = new List<SettlementItem>();
        foreach (var ticked in command.Items)
        {
            if (!owed.TryGetValue((ticked.Kind, ticked.Id), out var current) || current.Outstanding != ticked.Outstanding)
            {
                return Result.Failure<SettlementResponse>(SettlementErrors.DebtChanged);
            }

            items.Add(new SettlementItem(current.Kind, current.Id, current.StartDate, current.Outstanding));
        }

        var allocated = SettlementAllocator.Allocate(items, command.Amount);
        if (allocated.IsFailure)
        {
            return Result.Failure<SettlementResponse>(allocated.Error);
        }

        // The endpoint's policy requires an authenticated user, so this is a wiring bug if hit.
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Settle member debt was called without an authenticated user.");

        // One moment and one settlement id for every row, so the history shows them as the one
        // handover they were.
        var paidAt = time.GetUtcNow();
        var settlementId = Guid.CreateVersion7();
        var payments = new List<SettlementPaymentResponse>();
        foreach (var share in allocated.Value)
        {
            var registered = Register(share, command, userId, paidAt);
            if (registered.IsFailure)
            {
                return Result.Failure<SettlementResponse>(registered.Error);
            }

            registered.Value.JoinSettlement(settlementId);
            db.Payments.Add(registered.Value);
            payments.Add(new SettlementPaymentResponse(
                registered.Value.Id, share.Item.Kind, share.Item.Id, share.Amount, share.Item.Outstanding - share.Amount));
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var remainingDebt = debt.Sum(item => item.Outstanding) - command.Amount;

        return new SettlementResponse(command.Amount, command.Method, payments, remainingDebt);
    }

    private static Result<Payment> Register(
        SettlementShare share, SettleMemberDebtCommand command, Guid userId, DateTimeOffset paidAt) =>
        share.Item.Kind switch
        {
            PaymentTargetKind.Subscription => Payment.RegisterForSubscription(
                share.Item.Id, share.Amount, command.Method, command.ReferenceNumber, userId, paidAt),
            PaymentTargetKind.ServiceCharge => Payment.RegisterForServiceCharge(
                share.Item.Id, share.Amount, command.Method, command.ReferenceNumber, userId, paidAt),
            PaymentTargetKind.CafeOrder => Payment.RegisterForCafeOrder(
                share.Item.Id, share.Amount, command.Method, command.ReferenceNumber, userId, paidAt),
            _ => throw new InvalidOperationException($"Unknown payment target kind {share.Item.Kind}."),
        };
}
