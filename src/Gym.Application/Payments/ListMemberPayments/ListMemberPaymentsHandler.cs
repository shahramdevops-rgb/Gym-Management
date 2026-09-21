using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Domain.Common;
using Gym.Domain.Members;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Payments.ListMemberPayments;

/// <summary>A member's payment history across all of their subscriptions, newest first (task 4.5).</summary>
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

        // An inner join: a payment always has a subscription today (cafe order payments don't
        // exist until Phase 7), and it is exactly what "this member's payments" means.
        var payments =
            from payment in db.Payments.AsNoTracking()
            join subscription in db.Subscriptions.AsNoTracking() on payment.SubscriptionId equals subscription.Id
            where subscription.MemberId == memberId
            select new { payment, subscription.PlanName };

        var totalCount = await payments.CountAsync(cancellationToken);

        var items = await payments
            .OrderByDescending(row => row.payment.PaidAt)
            .ThenBy(row => row.payment.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(row => new PaymentHistoryResponse(
                row.payment.Id,
                row.payment.SubscriptionId!.Value,
                row.PlanName,
                row.payment.Kind,
                row.payment.Amount,
                row.payment.Method,
                row.payment.ReferenceNumber,
                row.payment.PaidAt,
                row.payment.ReceivedByUserId,
                row.payment.Reason,
                row.payment.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResponse<PaymentHistoryResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
