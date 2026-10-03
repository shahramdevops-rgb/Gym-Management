using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Application.Subscriptions;
using Gym.Domain.Common;
using Gym.Domain.Members;
using Gym.Domain.Payments;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Payments.ListMemberPayments;

/// <summary>
/// A member's payment history across everything they have paid for — subscriptions, service
/// charges and cafe orders — newest first (task 4.5, extended in 5.7 and 7.3).
/// </summary>
public sealed class ListMemberPaymentsHandler(IAppDbContext db)
{
    public async Task<Result<PagedResponse<PaymentHistoryResponse>>> Handle(
        Guid memberId, ListMemberPaymentsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var memberExists = await db.Members.AsNoTracking().AnyAsync(m => m.Id == memberId, cancellationToken);
        if (!memberExists)
        {
            return Result.Failure<PagedResponse<PaymentHistoryResponse>>(MemberErrors.NotFound);
        }

        // Filtered by "belongs to one of this member's items" rather than joined to them, because
        // a payment has three possible parents and a join would have to become a union. The
        // labels below are correlated subqueries for the same reason; at most one of them fires
        // per row, since a payment belongs to exactly one thing (BUSINESS_RULES.md §5). A cafe
        // order needs no label: its lines are on the order, and the kind says "cafe".
        var payments = db.Payments.AsNoTracking()
            .Where(payment =>
                db.Subscriptions.Any(subscription =>
                    subscription.Id == payment.SubscriptionId && subscription.MemberId == memberId) ||
                db.ServiceCharges.Any(charge =>
                    charge.Id == payment.ServiceChargeId && charge.MemberId == memberId) ||
                db.CafeOrders.Any(order =>
                    order.Id == payment.CafeOrderId && order.MemberId == memberId));

        var totalCount = await payments.CountAsync(cancellationToken);

        var page = await payments
            .OrderByDescending(payment => payment.PaidAt)
            .ThenBy(payment => payment.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(payment => new
            {
                payment.Id,
                TargetKind = payment.SubscriptionId != null
                    ? PaymentTargetKind.Subscription
                    : payment.ServiceChargeId != null ? PaymentTargetKind.ServiceCharge : PaymentTargetKind.CafeOrder,
                TargetId = payment.SubscriptionId != null
                    ? payment.SubscriptionId!.Value
                    : payment.ServiceChargeId != null ? payment.ServiceChargeId!.Value : payment.CafeOrderId!.Value,
                // A plan has no name (BUSINESS_RULES.md §3): the row carries its numbers and the
                // frontend labels it.
                SubscriptionPlan = db.Subscriptions
                    .Where(subscription => subscription.Id == payment.SubscriptionId)
                    .Select(subscription => new PlanSummary(
                        subscription.DurationDays, subscription.TotalSessions, subscription.IsSingleSession))
                    .FirstOrDefault(),
                ServiceKind = db.ServiceCharges
                    .Where(charge => charge.Id == payment.ServiceChargeId)
                    .Select(charge => (Domain.ServiceCharges.ServiceChargeKind?)charge.Kind)
                    .FirstOrDefault(),
                ServiceDescription = db.ServiceCharges
                    .Where(charge => charge.Id == payment.ServiceChargeId)
                    .Select(charge => charge.Description)
                    .FirstOrDefault(),
                payment.Kind,
                payment.Amount,
                payment.Method,
                payment.ReferenceNumber,
                payment.PaidAt,
                payment.ReceivedByUserId,
                payment.Reason,
                payment.CreatedAt,
                payment.SettlementId,
                // The whole handover's figures, as in the gym's history. A CASE, so a payment taken
                // on its own never runs the subqueries.
                SettlementTotal = payment.SettlementId == null
                    ? (decimal?)null
                    : db.Payments.Where(other => other.SettlementId == payment.SettlementId).Sum(other => other.Amount),
                SettlementItemCount = payment.SettlementId == null
                    ? (int?)null
                    : db.Payments.Count(other => other.SettlementId == payment.SettlementId),
            })
            .ToListAsync(cancellationToken);

        var items = page
            .Select(row => new PaymentHistoryResponse(
                row.Id,
                row.TargetKind,
                row.TargetId,
                row.SubscriptionPlan,
                row.ServiceKind,
                row.ServiceDescription,
                row.Kind,
                row.Amount,
                row.Method,
                row.ReferenceNumber,
                row.PaidAt,
                row.ReceivedByUserId,
                row.Reason,
                row.CreatedAt,
                row.SettlementId is { } settlementId
                    ? new SettlementSummary(settlementId, row.SettlementTotal!.Value, row.SettlementItemCount!.Value)
                    : null))
            .ToList();

        return new PagedResponse<PaymentHistoryResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
