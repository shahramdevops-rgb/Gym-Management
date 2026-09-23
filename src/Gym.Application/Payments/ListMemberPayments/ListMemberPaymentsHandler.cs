using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Domain.Common;
using Gym.Domain.Members;
using Gym.Domain.Payments;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Payments.ListMemberPayments;

/// <summary>
/// A member's payment history across everything they have paid for — subscriptions and service
/// charges — newest first (task 4.5, extended in 5.7).
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
        // a payment now has two possible parents and a join would have to become a union. The
        // labels below are correlated subqueries for the same reason; only one of them fires per
        // row, since a payment belongs to exactly one thing (BUSINESS_RULES.md §5).
        var payments = db.Payments.AsNoTracking()
            .Where(payment =>
                db.Subscriptions.Any(subscription =>
                    subscription.Id == payment.SubscriptionId && subscription.MemberId == memberId) ||
                db.ServiceCharges.Any(charge =>
                    charge.Id == payment.ServiceChargeId && charge.MemberId == memberId));

        var totalCount = await payments.CountAsync(cancellationToken);

        var items = await payments
            .OrderByDescending(payment => payment.PaidAt)
            .ThenBy(payment => payment.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(payment => new PaymentHistoryResponse(
                payment.Id,
                payment.SubscriptionId != null ? PaymentTargetKind.Subscription : PaymentTargetKind.ServiceCharge,
                payment.SubscriptionId != null ? payment.SubscriptionId!.Value : payment.ServiceChargeId!.Value,
                // The plan's name is read live rather than from the sale: renaming a plan corrects
                // the label on every receipt it has ever appeared on (BUSINESS_RULES.md §4).
                db.Subscriptions
                    .Where(subscription => subscription.Id == payment.SubscriptionId)
                    .Select(subscription => db.Plans
                        .Where(plan => plan.Id == subscription.PlanId)
                        .Select(plan => plan.Name)
                        .FirstOrDefault())
                    .FirstOrDefault(),
                db.ServiceCharges
                    .Where(charge => charge.Id == payment.ServiceChargeId)
                    .Select(charge => (Domain.ServiceCharges.ServiceChargeKind?)charge.Kind)
                    .FirstOrDefault(),
                payment.Kind,
                payment.Amount,
                payment.Method,
                payment.ReferenceNumber,
                payment.PaidAt,
                payment.ReceivedByUserId,
                payment.Reason,
                payment.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResponse<PaymentHistoryResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
