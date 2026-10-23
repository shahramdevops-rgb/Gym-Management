using Gym.Application.Common;
using Gym.Application.History.ListSales;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Notifications.SendDailySms;

/// <summary>
/// The debt reminders a run is to send today (BUSINESS_RULES.md §10 <i>The debt reminder</i>, task
/// 10.11), built but not yet saved.
/// </summary>
/// <remarks>
/// <para>
/// <b>The receivables' own sales.</b> What a member owes, and since when, comes from
/// <see cref="SaleRows"/>, the query «مطالبات» and «بدهی‌های قدیمی» read (§12), so the amount in
/// the SMS is the one the reports and the desk show. A guest's or a walk-in's sale has no member and
/// is left out: there is nobody to text.
/// </para>
/// <para>
/// <b>The query narrows, <see cref="SmsAudience"/> decides</b>, as for the other kinds: each member's
/// debt and their latest reminder are read here, and the rule says whether this debt has had one.
/// The unique index on the member and the debt's first day is what really keeps a reminder from
/// being paid for twice.
/// </para>
/// <para>
/// Deactivated members are not left out (decided with the developer, 1405/07/27): money owed is owed.
/// </para>
/// </remarks>
public sealed class DebtReminderCandidate(IAppDbContext db, SaleRows saleRows, IGymCalendar calendar)
{
    public async Task<List<Notification>> FindAsync(SmsSettings settings, DateOnly today, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // The database holds "on means filled", and a run only starts for a kind that is on.
        var daysAfter = settings.DebtReminderDaysAfter
            ?? throw new InvalidOperationException("The debt reminder is on with no number of days.");
        var minAmount = settings.DebtReminderMinAmount
            ?? throw new InvalidOperationException("The debt reminder is on with no minimum amount.");

        // A cancelled or voided sale owes nothing (§5), and a paid one has nothing left.
        var debts = await saleRows.Matching(new EverySale())
            .Where(sale => sale.MemberId != null && sale.UndoneAt == null && sale.NetPaid < sale.Amount)
            .GroupBy(sale => sale.MemberId!.Value)
            .Select(sales => new
            {
                MemberId = sales.Key,
                Owed = sales.Sum(sale => sale.Amount - sale.NetPaid),
                OldestSoldAt = sales.Min(sale => sale.SoldAt),
            })
            .ToListAsync(cancellationToken);

        if (debts.Count == 0)
        {
            return [];
        }

        var memberIds = debts.Select(debt => debt.MemberId).ToList();

        var members = await db.Members
            .AsNoTracking()
            .Where(member => memberIds.Contains(member.Id))
            .Select(member => new { member.Id, member.FullName, member.PhoneNumber })
            .ToDictionaryAsync(member => member.Id, cancellationToken);

        // Whatever became of it: a failed reminder still counts as written (the Owner can resend it).
        var lastReminders = await db.Notifications
            .AsNoTracking()
            .Where(notification => notification.Kind == NotificationKind.DebtReminder && memberIds.Contains(notification.MemberId!.Value))
            .GroupBy(notification => notification.MemberId!.Value)
            .Select(reminders => new { MemberId = reminders.Key, WrittenAt = reminders.Max(notification => notification.CreatedAt) })
            .ToDictionaryAsync(reminder => reminder.MemberId, reminder => reminder.WrittenAt, cancellationToken);

        return debts
            .Select(row => new
            {
                Member = members[row.MemberId],
                Debt = new OpenDebt(row.Owed, row.OldestSoldAt, calendar.DayOf(row.OldestSoldAt)),
            })
            .Where(row => SmsAudience.IsDebtReminderDue(
                row.Debt,
                lastReminders.TryGetValue(row.Member.Id, out var writtenAt) ? writtenAt : null,
                today,
                daysAfter,
                minAmount))
            .OrderBy(row => row.Debt.OldestSoldAt)
            .ThenBy(row => row.Member.Id)
            .Select(row => Notification.ForDebtReminder(
                row.Member.Id,
                row.Debt.OldestSoldOn,
                row.Member.PhoneNumber,
                SmsText.ForDebtReminder(row.Member.FullName, row.Debt.Owed)))
            .ToList();
    }

    /// <summary>No range, no member, every kind: everything ever sold.</summary>
    private sealed record EverySale(
        DateOnly? From = null,
        DateOnly? To = null,
        Guid? MemberId = null,
        SaleSource? Source = null,
        SalePaidFilter? Paid = null) : ISalesFilter;
}
