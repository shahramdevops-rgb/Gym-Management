using Gym.Application.Common;
using Gym.Domain.Payments;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.History.ListPayments;

/// <summary>
/// Every payment and refund a filter lets through, as one query the database runs: the payments
/// list pages it (<see cref="ListPaymentsHandler"/>), the totals add it up
/// (<c>PaymentTotalsHandler</c>, roadmap 6.5.32). One place decides which rows count, so the two
/// can never disagree.
/// </summary>
/// <remarks>
/// How far back Staff may reach is not a filter: it is the list handler's check, because it
/// depends on who is asking (<see cref="PaymentHistoryWindow"/>).
/// </remarks>
public sealed class PaymentRows(IAppDbContext db, IGymCalendar calendar)
{
    /// <summary>The payments and refunds <paramref name="filter"/> asks for, not yet sorted.</summary>
    public IQueryable<Payment> Matching(IPaymentsFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var payments = db.Payments.AsNoTracking();

        // A payment belongs to the day it was taken, in the gym's time zone (§5 revenue, §12).
        if (filter.From is { } from)
        {
            var start = calendar.StartOfDayUtc(from);
            payments = payments.Where(payment => payment.PaidAt >= start);
        }

        if (filter.To is { } to)
        {
            var end = calendar.StartOfDayUtc(to.AddDays(1));
            payments = payments.Where(payment => payment.PaidAt < end);
        }

        if (filter.Method is { } method)
        {
            payments = payments.Where(payment => payment.Method == method);
        }

        payments = filter.Source switch
        {
            PaymentTargetKind.Subscription => payments.Where(payment => payment.SubscriptionId != null),
            PaymentTargetKind.ServiceCharge => payments.Where(payment => payment.ServiceChargeId != null),
            PaymentTargetKind.CafeOrder => payments.Where(payment => payment.CafeOrderId != null),
            _ => payments,
        };

        // هوازی, فروشگاه and آنالیز are all service charges, but the screen lists them as separate sources.
        if (filter.Source == PaymentTargetKind.ServiceCharge && filter.ServiceKind is { } serviceKind)
        {
            payments = payments.Where(payment =>
                db.ServiceCharges.Any(charge => charge.Id == payment.ServiceChargeId && charge.Kind == serviceKind));
        }

        // "Belongs to one of this member's items" rather than a join, because a payment has three
        // possible parents — the same filter as the member's own payment history.
        if (filter.MemberId is { } memberId)
        {
            payments = payments.Where(payment =>
                db.Subscriptions.Any(subscription =>
                    subscription.Id == payment.SubscriptionId && subscription.MemberId == memberId) ||
                db.ServiceCharges.Any(charge =>
                    charge.Id == payment.ServiceChargeId && charge.MemberId == memberId) ||
                db.CafeOrders.Any(order =>
                    order.Id == payment.CafeOrderId && order.MemberId == memberId));
        }

        return payments;
    }
}
