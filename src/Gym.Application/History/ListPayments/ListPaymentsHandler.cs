using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Application.Payments;
using Gym.Application.Subscriptions;
using Gym.Domain.Common;
using Gym.Domain.Payments;
using Gym.Domain.ServiceCharges;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.History.ListPayments;

/// <summary>
/// Every payment and refund the gym has taken or given, newest first, with who did it
/// (BUSINESS_RULES.md §12 <i>History</i>, roadmap 6.5.25). Staff reach back only to today and the
/// 3 days before it; the Owner has no limit.
/// </summary>
public sealed class ListPaymentsHandler(
    IAppDbContext db, PaymentRows paymentRows, IGymCalendar calendar, ICurrentUser currentUser, IUserNames users)
{
    public async Task<Result<PagedResponse<HistoryPaymentResponse>>> Handle(
        ListPaymentsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Enforced here, not only by the screen's date box: the API is what keeps older money
        // out of Staff's reach (§1).
        var window = PaymentHistoryWindow.Check(query.From, currentUser.IsOwner, calendar.Today());
        if (window.IsFailure)
        {
            return Result.Failure<PagedResponse<HistoryPaymentResponse>>(window.Error);
        }

        var payments = paymentRows.Matching(query);

        var totalCount = await payments.CountAsync(cancellationToken);

        // The labels are correlated subqueries; at most one of each trio finds a row, since a
        // payment belongs to exactly one thing (§5).
        var page = await payments
            .OrderByDescending(payment => payment.PaidAt)
            .ThenByDescending(payment => payment.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(payment => new
            {
                payment.Id,
                payment.SubscriptionId,
                payment.ServiceChargeId,
                payment.CafeOrderId,
                SubscriptionMemberId = db.Subscriptions
                    .Where(subscription => subscription.Id == payment.SubscriptionId)
                    .Select(subscription => (Guid?)subscription.MemberId)
                    .FirstOrDefault(),
                ServiceChargeMemberId = db.ServiceCharges
                    .Where(charge => charge.Id == payment.ServiceChargeId)
                    .Select(charge => charge.MemberId)
                    .FirstOrDefault(),
                CafeOrderMemberId = db.CafeOrders
                    .Where(order => order.Id == payment.CafeOrderId)
                    .Select(order => order.MemberId)
                    .FirstOrDefault(),
                // A guest's cafe order or charge names their visit and no member (§7 Guest visit, §8):
                // the name is on the visit. At most one of the two finds a row.
                ChargeGuestName = db.ServiceCharges
                    .Where(charge => charge.Id == payment.ServiceChargeId && charge.MemberId == null)
                    .Select(charge => db.Attendances
                        .Where(attendance => attendance.Id == charge.AttendanceId)
                        .Select(attendance => attendance.GuestName)
                        .FirstOrDefault())
                    .FirstOrDefault(),
                CafeGuestName = db.CafeOrders
                    .Where(order => order.Id == payment.CafeOrderId)
                    .Select(order => db.Attendances
                        .Where(attendance => attendance.Id == order.AttendanceId)
                        .Select(attendance => attendance.GuestName)
                        .FirstOrDefault())
                    .FirstOrDefault(),
                SubscriptionPlan = db.Subscriptions
                    .Where(subscription => subscription.Id == payment.SubscriptionId)
                    .Select(subscription => new PlanSummary(
                        subscription.DurationDays, subscription.TotalSessions, subscription.IsSingleSession))
                    .FirstOrDefault(),
                ServiceKind = db.ServiceCharges
                    .Where(charge => charge.Id == payment.ServiceChargeId)
                    .Select(charge => (ServiceChargeKind?)charge.Kind)
                    .FirstOrDefault(),
                ServiceDescription = db.ServiceCharges
                    .Where(charge => charge.Id == payment.ServiceChargeId)
                    .Select(charge => charge.Description)
                    .FirstOrDefault(),
                TargetUndone =
                    db.Subscriptions.Any(subscription =>
                        subscription.Id == payment.SubscriptionId && subscription.CancelledAt != null) ||
                    db.ServiceCharges.Any(charge =>
                        charge.Id == payment.ServiceChargeId && charge.VoidedAt != null) ||
                    db.CafeOrders.Any(order =>
                        order.Id == payment.CafeOrderId && order.CancelledAt != null),
                payment.Kind,
                payment.Amount,
                payment.Method,
                payment.ReferenceNumber,
                payment.Reason,
                payment.PaidAt,
                payment.ReceivedByUserId,
                payment.SettlementId,
                // The whole handover's figures, whatever the filters let through. A CASE, so a
                // payment taken on its own never runs the subqueries.
                SettlementTotal = payment.SettlementId == null
                    ? (decimal?)null
                    : db.Payments.Where(other => other.SettlementId == payment.SettlementId).Sum(other => other.Amount),
                SettlementItemCount = payment.SettlementId == null
                    ? (int?)null
                    : db.Payments.Count(other => other.SettlementId == payment.SettlementId),
            })
            .ToListAsync(cancellationToken);

        var rows = page
            .Select(row => new
            {
                Payment = row,
                MemberId = row.SubscriptionMemberId ?? row.ServiceChargeMemberId ?? row.CafeOrderMemberId,
            })
            .ToList();

        // Two batched lookups for the whole page, not one per row: whose money, and who took it.
        var memberIds = rows.Where(row => row.MemberId is not null).Select(row => row.MemberId!.Value)
            .Distinct().ToList();
        var memberNames = await db.Members.AsNoTracking()
            .Where(member => memberIds.Contains(member.Id))
            .ToDictionaryAsync(member => member.Id, member => member.FullName, cancellationToken);

        var userNames = await users.FullNamesAsync(
            rows.Select(row => row.Payment.ReceivedByUserId).Distinct().ToList(), cancellationToken);

        var items = rows
            .Select(row => new HistoryPaymentResponse(
                row.Payment.Id,
                SourceOf(row.Payment.SubscriptionId, row.Payment.ServiceChargeId),
                row.Payment.SubscriptionId ?? row.Payment.ServiceChargeId ?? row.Payment.CafeOrderId!.Value,
                row.MemberId,
                row.MemberId is { } id ? memberNames.GetValueOrDefault(id) : null,
                row.Payment.CafeGuestName ?? row.Payment.ChargeGuestName,
                row.Payment.SubscriptionPlan,
                row.Payment.ServiceKind,
                row.Payment.ServiceDescription,
                row.Payment.Kind,
                row.Payment.Amount,
                row.Payment.Method,
                row.Payment.ReferenceNumber,
                row.Payment.Reason,
                row.Payment.PaidAt,
                row.Payment.TargetUndone,
                userNames.GetValueOrDefault(row.Payment.ReceivedByUserId),
                row.Payment.SettlementId is { } settlementId
                    ? new SettlementSummary(
                        settlementId, row.Payment.SettlementTotal!.Value, row.Payment.SettlementItemCount!.Value)
                    : null))
            .ToList();

        return new PagedResponse<HistoryPaymentResponse>(items, query.Page, query.PageSize, totalCount);
    }

    private static PaymentTargetKind SourceOf(Guid? subscriptionId, Guid? serviceChargeId) =>
        subscriptionId is not null ? PaymentTargetKind.Subscription
        : serviceChargeId is not null ? PaymentTargetKind.ServiceCharge
        : PaymentTargetKind.CafeOrder;
}
