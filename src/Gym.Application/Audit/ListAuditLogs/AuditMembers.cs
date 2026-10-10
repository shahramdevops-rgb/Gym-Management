using Gym.Application.Common;
using Gym.Domain.Attendances;
using Gym.Domain.Audit;
using Gym.Domain.Cafe;
using Gym.Domain.Members;
using Gym.Domain.Notifications;
using Gym.Domain.Payments;
using Gym.Domain.ServiceCharges;
using Gym.Domain.Subscriptions;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Audit.ListAuditLogs;

/// <summary>
/// Which member an audit row belongs to (BUSINESS_RULES.md §11 <i>The audit screen</i>): the member
/// itself, or a record that names one, directly or through the record a payment or an order line is for.
/// </summary>
/// <remarks>
/// <para>
/// The log keeps only a record's kind and key, never its member, so the answer is read from the
/// records as they are now. A record's member never changes once it is written, so "now" and "then"
/// agree.
/// </para>
/// <para>
/// One rule, used twice: <see cref="OfMember"/> filters the log by member in the database, and
/// <see cref="ForPageAsync"/> names the members of one page of rows. Both follow the same links, so
/// the filter finds exactly the rows that show the member's name.
/// </para>
/// </remarks>
public static class AuditMembers
{
    private const string MemberType = nameof(Member);
    private const string SubscriptionType = nameof(Subscription);
    private const string AttendanceType = nameof(Attendance);
    private const string ServiceChargeType = nameof(ServiceCharge);
    private const string CafeOrderType = nameof(CafeOrder);
    private const string CafeOrderItemType = nameof(CafeOrderItem);
    private const string NotificationType = nameof(Notification);
    private const string PaymentType = nameof(Payment);

    /// <summary>The rows of one member, as a filter the database runs.</summary>
    /// <remarks>
    /// The log stores a key as text, so each kind's ids become text in the subquery
    /// (<c>id::text</c>) and are matched with the <c>(entity_type, entity_id)</c> index.
    /// </remarks>
    public static IQueryable<AuditLog> OfMember(IAppDbContext db, IQueryable<AuditLog> logs, Guid memberId)
    {
        var memberKey = memberId.ToString();

        var subscriptions = db.Subscriptions.Where(subscription => subscription.MemberId == memberId).Select(subscription => subscription.Id);
        var attendances = db.Attendances.Where(attendance => attendance.MemberId == memberId).Select(attendance => attendance.Id);
        var charges = db.ServiceCharges.Where(charge => charge.MemberId == memberId).Select(charge => charge.Id);
        var orders = db.CafeOrders.Where(order => order.MemberId == memberId).Select(order => order.Id);
        var items = db.CafeOrderItems.Where(item => orders.Contains(item.OrderId)).Select(item => item.Id);
        var notifications = db.Notifications.Where(notification => notification.MemberId == memberId).Select(notification => notification.Id);
        var payments = db.Payments
            .Where(payment =>
                (payment.SubscriptionId != null && subscriptions.Contains(payment.SubscriptionId.Value)) ||
                (payment.ServiceChargeId != null && charges.Contains(payment.ServiceChargeId.Value)) ||
                (payment.CafeOrderId != null && orders.Contains(payment.CafeOrderId.Value)))
            .Select(payment => payment.Id);

        return logs.Where(log =>
            (log.EntityType == MemberType && log.EntityId == memberKey) ||
            (log.EntityType == SubscriptionType && subscriptions.Select(id => id.ToString()).Contains(log.EntityId)) ||
            (log.EntityType == AttendanceType && attendances.Select(id => id.ToString()).Contains(log.EntityId)) ||
            (log.EntityType == ServiceChargeType && charges.Select(id => id.ToString()).Contains(log.EntityId)) ||
            (log.EntityType == CafeOrderType && orders.Select(id => id.ToString()).Contains(log.EntityId)) ||
            (log.EntityType == CafeOrderItemType && items.Select(id => id.ToString()).Contains(log.EntityId)) ||
            (log.EntityType == NotificationType && notifications.Select(id => id.ToString()).Contains(log.EntityId)) ||
            (log.EntityType == PaymentType && payments.Select(id => id.ToString()).Contains(log.EntityId)));
    }

    /// <summary>
    /// The member of each row on one page, with the member's name, keyed by the audit row's id. A row
    /// that belongs to no member (a product, a guest's visit, a walk-in's order) is missing.
    /// </summary>
    /// <remarks>A few queries per page, one per kind on it, never one per row.</remarks>
    public static async Task<IReadOnlyDictionary<Guid, (Guid MemberId, string FullName)>> ForPageAsync(
        IAppDbContext db, IReadOnlyCollection<AuditLog> rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rows);

        // A payment and an order line point at another record; those are looked up with the rest.
        var payments = await LookupAsync(
            KeysOf(rows, PaymentType),
            ids => db.Payments.Where(payment => ids.Contains(payment.Id))
                .Select(payment => new { payment.Id, payment.SubscriptionId, payment.ServiceChargeId, payment.CafeOrderId }),
            payment => payment.Id,
            cancellationToken);
        var items = await LookupAsync(
            KeysOf(rows, CafeOrderItemType),
            ids => db.CafeOrderItems.Where(item => ids.Contains(item.Id)).Select(item => new { item.Id, item.OrderId }),
            item => item.Id,
            cancellationToken);

        var subscriptionIds = KeysOf(rows, SubscriptionType)
            .Concat(payments.Values.Where(payment => payment.SubscriptionId is not null).Select(payment => payment.SubscriptionId!.Value));
        var chargeIds = KeysOf(rows, ServiceChargeType)
            .Concat(payments.Values.Where(payment => payment.ServiceChargeId is not null).Select(payment => payment.ServiceChargeId!.Value));
        var orderIds = KeysOf(rows, CafeOrderType)
            .Concat(payments.Values.Where(payment => payment.CafeOrderId is not null).Select(payment => payment.CafeOrderId!.Value))
            .Concat(items.Values.Select(item => item.OrderId));

        var subscriptionMembers = await MembersOfAsync(
            subscriptionIds,
            ids => db.Subscriptions.Where(subscription => ids.Contains(subscription.Id))
                .Select(subscription => new KeyValuePair<Guid, Guid?>(subscription.Id, subscription.MemberId)),
            cancellationToken);
        var attendanceMembers = await MembersOfAsync(
            KeysOf(rows, AttendanceType),
            ids => db.Attendances.Where(attendance => ids.Contains(attendance.Id))
                .Select(attendance => new KeyValuePair<Guid, Guid?>(attendance.Id, attendance.MemberId)),
            cancellationToken);
        var chargeMembers = await MembersOfAsync(
            chargeIds,
            ids => db.ServiceCharges.Where(charge => ids.Contains(charge.Id))
                .Select(charge => new KeyValuePair<Guid, Guid?>(charge.Id, charge.MemberId)),
            cancellationToken);
        var orderMembers = await MembersOfAsync(
            orderIds,
            ids => db.CafeOrders.Where(order => ids.Contains(order.Id))
                .Select(order => new KeyValuePair<Guid, Guid?>(order.Id, order.MemberId)),
            cancellationToken);
        var notificationMembers = await MembersOfAsync(
            KeysOf(rows, NotificationType),
            ids => db.Notifications.Where(notification => ids.Contains(notification.Id))
                .Select(notification => new KeyValuePair<Guid, Guid?>(notification.Id, notification.MemberId)),
            cancellationToken);

        var memberOfRow = new Dictionary<Guid, Guid>();
        foreach (var row in rows)
        {
            if (!Guid.TryParse(row.EntityId, out var key))
            {
                continue;
            }

            var memberId = row.EntityType switch
            {
                MemberType => key,
                SubscriptionType => subscriptionMembers.GetValueOrDefault(key),
                AttendanceType => attendanceMembers.GetValueOrDefault(key),
                ServiceChargeType => chargeMembers.GetValueOrDefault(key),
                CafeOrderType => orderMembers.GetValueOrDefault(key),
                CafeOrderItemType => items.TryGetValue(key, out var item) ? orderMembers.GetValueOrDefault(item.OrderId) : null,
                NotificationType => notificationMembers.GetValueOrDefault(key),
                PaymentType => payments.TryGetValue(key, out var payment) ? PaymentMember(payment.SubscriptionId, payment.ServiceChargeId, payment.CafeOrderId) : null,
                _ => (Guid?)null,
            };

            if (memberId is { } found)
            {
                memberOfRow[row.Id] = found;
            }
        }

        var memberIds = memberOfRow.Values.Distinct().ToList();
        var names = memberIds.Count == 0
            ? []
            : await db.Members.AsNoTracking()
                .Where(member => memberIds.Contains(member.Id))
                .ToDictionaryAsync(member => member.Id, member => member.FullName, cancellationToken);

        // A member that is gone (never, today: members are deactivated, not deleted) names no one.
        return memberOfRow
            .Where(pair => names.ContainsKey(pair.Value))
            .ToDictionary(pair => pair.Key, pair => (MemberId: pair.Value, FullName: names[pair.Value]));

        Guid? PaymentMember(Guid? subscriptionId, Guid? chargeId, Guid? orderId) =>
            subscriptionId is { } subscription ? subscriptionMembers.GetValueOrDefault(subscription)
            : chargeId is { } charge ? chargeMembers.GetValueOrDefault(charge)
            : orderId is { } order ? orderMembers.GetValueOrDefault(order)
            : null;
    }

    private static List<Guid> KeysOf(IEnumerable<AuditLog> rows, string entityType) =>
        rows.Where(row => row.EntityType == entityType)
            .Select(row => Guid.TryParse(row.EntityId, out var key) ? key : (Guid?)null)
            .OfType<Guid>()
            .ToList();

    private static async Task<Dictionary<Guid, T>> LookupAsync<T>(
        List<Guid> ids,
        Func<List<Guid>, IQueryable<T>> query,
        Func<T, Guid> keyOf,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var found = await query(ids).ToListAsync(cancellationToken);

        return found.ToDictionary(keyOf);
    }

    private static async Task<Dictionary<Guid, Guid?>> MembersOfAsync(
        IEnumerable<Guid> ids,
        Func<List<Guid>, IQueryable<KeyValuePair<Guid, Guid?>>> query,
        CancellationToken cancellationToken)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0)
        {
            return [];
        }

        var found = await query(distinct).ToListAsync(cancellationToken);

        return found.ToDictionary(pair => pair.Key, pair => pair.Value);
    }
}
