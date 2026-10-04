using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Domain.Payments;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.GuestDebts.ListGuestDebts;

/// <summary>
/// «بدهی مهمان‌ها»: every cafe order and service charge of a guest's visit that still owes money,
/// newest first (BUSINESS_RULES.md §7 <i>Guest visit</i>, roadmap 6.5.31). Mostly what the nightly
/// job closed unpaid, but a guest still inside is listed too. Staff or Owner: it is front-desk work.
/// </summary>
/// <remarks>
/// <para>
/// A guest's purchase is the one with a visit and no member, in either table: a walk-in's cafe
/// order has neither, and a member's has both.
/// </para>
/// <para>
/// The two tables are put together in the database as one <c>UNION ALL</c> and filtered, counted
/// and paged there, the way <c>ListSalesHandler</c> does it, so "unpaid" never has to be worked out
/// in memory. What each row says is read afterwards for the page only.
/// </para>
/// </remarks>
public sealed class ListGuestDebtsHandler(IAppDbContext db)
{
    public async Task<PagedResponse<GuestDebtResponse>> Handle(
        ListGuestDebtsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var debts = Charges().Concat(CafeOrders())
            .Where(debt => debt.NetPaid < debt.Amount);

        var totalCount = await debts.CountAsync(cancellationToken);

        var page = await debts
            .OrderByDescending(debt => debt.RecordedAt)
            .ThenByDescending(debt => debt.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = await DescribeAsync(page, cancellationToken);

        return new PagedResponse<GuestDebtResponse>(items, query.Page, query.PageSize, totalCount);
    }

    /// <summary>A guest's standing charges: no member, not voided (a voided one owes nothing).</summary>
    private IQueryable<DebtRow> Charges() =>
        db.ServiceCharges.AsNoTracking()
            .Where(charge => charge.MemberId == null && charge.VoidedAt == null)
            .Select(charge => new DebtRow
            {
                Target = PaymentTargetKind.ServiceCharge,
                Id = charge.Id,
                AttendanceId = charge.AttendanceId,
                Amount = charge.Amount,
                NetPaid = db.Payments
                    .Where(payment => payment.ServiceChargeId == charge.Id)
                    .Sum(payment => payment.Kind == PaymentKind.Payment ? payment.Amount : -payment.Amount),
                RecordedAt = charge.CreatedAt,
            });

    /// <summary>A guest's standing cafe orders: a visit and no member, not cancelled.</summary>
    private IQueryable<DebtRow> CafeOrders() =>
        db.CafeOrders.AsNoTracking()
            .Where(order => order.MemberId == null && order.AttendanceId != null && order.CancelledAt == null)
            .Select(order => new DebtRow
            {
                Target = PaymentTargetKind.CafeOrder,
                Id = order.Id,
                AttendanceId = order.AttendanceId!.Value,
                Amount = order.TotalAmount,
                NetPaid = db.Payments
                    .Where(payment => payment.CafeOrderId == order.Id)
                    .Sum(payment => payment.Kind == PaymentKind.Payment ? payment.Amount : -payment.Amount),
                RecordedAt = order.CreatedAt,
            });

    /// <summary>What the page's rows say on screen: one query per table, then the visits.</summary>
    private async Task<List<GuestDebtResponse>> DescribeAsync(List<DebtRow> page, CancellationToken cancellationToken)
    {
        var chargeIds = page.Where(debt => debt.Target == PaymentTargetKind.ServiceCharge).Select(debt => debt.Id).ToList();
        var orderIds = page.Where(debt => debt.Target == PaymentTargetKind.CafeOrder).Select(debt => debt.Id).ToList();
        var visitIds = page.Select(debt => debt.AttendanceId).Distinct().ToList();

        var charges = await db.ServiceCharges.AsNoTracking()
            .Where(charge => chargeIds.Contains(charge.Id))
            .ToDictionaryAsync(charge => charge.Id, cancellationToken);

        var orderDays = await db.CafeOrders.AsNoTracking()
            .Where(order => orderIds.Contains(order.Id))
            .ToDictionaryAsync(order => order.Id, order => order.OrderedOn, cancellationToken);

        var orderItems = await db.CafeOrderItems.AsNoTracking()
            .Where(item => orderIds.Contains(item.OrderId))
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .Select(item => new { item.OrderId, item.ProductName, item.Quantity })
            .ToListAsync(cancellationToken);
        var itemsByOrder = orderItems.ToLookup(item => item.OrderId, item => new GuestDebtCafeItem(item.ProductName, item.Quantity));

        var visits = await db.Attendances.AsNoTracking()
            .Where(attendance => visitIds.Contains(attendance.Id))
            .Select(attendance => new { attendance.Id, attendance.GuestName, IsOpen = attendance.CheckedOutAt == null })
            .ToDictionaryAsync(attendance => attendance.Id, cancellationToken);

        return page
            .Select(debt =>
            {
                var visit = visits[debt.AttendanceId];
                var charge = debt.Target == PaymentTargetKind.ServiceCharge ? charges[debt.Id] : null;

                return new GuestDebtResponse(
                    debt.Target,
                    debt.Id,
                    debt.AttendanceId,
                    visit.GuestName ?? string.Empty,
                    charge?.ChargedOn ?? orderDays[debt.Id],
                    debt.RecordedAt,
                    charge?.Kind,
                    charge?.Description,
                    charge?.Quantity,
                    charge is null ? [.. itemsByOrder[debt.Id]] : null,
                    debt.Amount,
                    debt.NetPaid,
                    debt.Amount - debt.NetPaid,
                    PaymentStatusCalculator.Calculate(debt.Amount, debt.NetPaid),
                    visit.IsOpen);
            })
            .ToList();
    }

    /// <summary>
    /// What the union carries: enough to filter by unpaid, sort and page. A class with settable
    /// members, not a record, because EF Core matches the branches of a set operation member by member.
    /// </summary>
    private sealed class DebtRow
    {
        public PaymentTargetKind Target { get; init; }

        public Guid Id { get; init; }

        public Guid AttendanceId { get; init; }

        public decimal Amount { get; init; }

        public decimal NetPaid { get; init; }

        public DateTimeOffset RecordedAt { get; init; }
    }
}
