using Gym.Application.Cafe;
using Gym.Application.Common;
using Gym.Application.ServiceCharges;
using Gym.Domain.Attendances;
using Gym.Domain.Common;
using Gym.Domain.ServiceCharges;
using Gym.Domain.Subscriptions;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Attendances.CancelCheckIn;

/// <summary>
/// Cancels an open check-in within the allowed window (BUSINESS_RULES.md §7): restores the
/// session (a guest's visit has none), frees the locker (occupancy is derived from <c>CheckedOutAt</c>, which
/// <see cref="Attendance.Cancel"/> also sets), and cancels the purchases the desk ticked —
/// the هوازی, each sale named (فروشگاه, آنالیز) and each cafe order named. Front desk work, so both
/// roles.
/// </summary>
/// <remarks>
/// Before roadmap 6.5.8 the هوازی was always voided and the cafe never touched. The Owner made
/// both the desk's call, one purchase at a time: the member may have used the treadmill or taken
/// a drink and still had to leave. So the request carries the choice and nothing here guesses.
/// </remarks>
public sealed class CancelCheckInHandler(IAppDbContext db, IAttendancePolicy policy, TimeProvider time, ICurrentUser currentUser)
{
    /// <summary>
    /// Stored on the charges and orders this cancellation undoes. English like every other value
    /// the application writes itself; the reasons staff type are their own words.
    /// </summary>
    private const string CancelReason = "The check-in was cancelled.";

    public async Task<Result<AttendanceResponse>> Handle(Guid id, CancelCheckInCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var attendance = await db.Attendances.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (attendance is null)
        {
            return Result.Failure<AttendanceResponse>(AttendanceErrors.NotFound);
        }

        var now = time.GetUtcNow();

        // The endpoint's policy requires an authenticated user, so this is a wiring bug if hit.
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Cancel check-in was called without an authenticated user.");

        // One transaction, taking the member's lock, because the purchases below are refunded from
        // what has been paid against them and a payment for this member may be committing right
        // now — the same lock registering that payment takes. A guest has no member row, so their
        // visit's own row is the lock, which a guest's order and its payments take too.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (attendance.MemberId is { } memberId)
        {
            await db.LockMemberAsync(memberId, cancellationToken);
        }
        else
        {
            await db.LockAttendanceAsync(id, cancellationToken);
        }

        // A guest leaves no debt behind (BUSINESS_RULES.md §7 Guest visit): an order, a هوازی or a
        // sale the desk did not tick stays standing, so it must already be paid.
        var cafeOrderIds = command.CafeOrderIds ?? [];
        var saleIds = command.SaleIds ?? [];
        var leavesUnpaidPurchases = attendance.IsGuest
            && LeavesUnpaid(
                await GuestPurchases.UnpaidAsync(db, id, cancellationToken), command.VoidCardio == true, cafeOrderIds, saleIds);

        var cancelled = attendance.Cancel(now, policy.CancelWindowMinutes, leavesUnpaidPurchases);
        if (cancelled.IsFailure)
        {
            return Result.Failure<AttendanceResponse>(cancelled.Error);
        }

        // A guest's visit and a cardio-only one consumed no session, so there is none to give back
        // (BUSINESS_RULES.md §7). A cardio-only visit names its plan all the same.
        if (attendance.SubscriptionId is { } subscriptionId && !attendance.IsCardioOnly)
        {
            var subscription = await db.Subscriptions.SingleAsync(s => s.Id == subscriptionId, cancellationToken);
            subscription.RestoreSession();
        }

        if (command.VoidCardio == true)
        {
            await VoidCardioAsync(id, userId, now, cancellationToken);
        }

        var salesVoided = await VoidSalesAsync(
            id, saleIds, userId, now, cancellationToken);
        if (salesVoided.IsFailure)
        {
            // Nothing is saved: the transaction is disposed without a commit.
            return Result.Failure<AttendanceResponse>(salesVoided.Error);
        }

        var cafeCancelled = await CancelCafeOrdersAsync(id, cafeOrderIds, userId, now, cancellationToken);
        if (cafeCancelled.IsFailure)
        {
            // Nothing is saved: the transaction is disposed without a commit.
            return Result.Failure<AttendanceResponse>(cafeCancelled.Error);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The visit, its subscription or one of the orders changed since they were read (an
            // xmin changed). Nothing was saved; trying again reads them afresh.
            return Result.Failure<AttendanceResponse>(AttendanceErrors.ChangedConcurrently);
        }

        var lockerNumber = attendance.LockerId is null
            ? null
            : await db.Lockers.Where(l => l.Id == attendance.LockerId).Select(l => (int?)l.Number).SingleOrDefaultAsync(cancellationToken);

        // A هوازی the desk kept is still owed, so it stays on the visit, as on every other list.
        var charges = await VisitServiceCharges.ByAttendanceAsync(db, [id], cancellationToken);

        return AttendanceResponse.From(attendance, lockerNumber, charges.GetValueOrDefault(id));
    }

    /// <summary>
    /// Whether a purchase the desk left unticked still owes money: the هوازی unless it is voided,
    /// each sale and cafe order not named.
    /// </summary>
    private static bool LeavesUnpaid(
        GuestPurchases.Unpaid unpaid, bool voidCardio, IReadOnlyList<Guid> cafeOrderIds, IReadOnlyList<Guid> saleIds) =>
        unpaid.Orders.Any(order => !cafeOrderIds.Contains(order.Order.Id))
        || unpaid.Charges.Any(charge => charge.Charge.Kind == ServiceChargeKind.Cardio
            ? !voidCardio
            : !saleIds.Contains(charge.Charge.Id));

    /// <summary>
    /// BUSINESS_RULES.md §7 <i>Gym services</i>: voided with the check-in's reason, and anything
    /// already collected goes back the way it came (§5: no wallet, no credit balance).
    /// </summary>
    private async Task VoidCardioAsync(Guid attendanceId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var charges = await db.ServiceCharges
            .Where(charge => charge.AttendanceId == attendanceId
                && charge.Kind == ServiceChargeKind.Cardio
                && charge.VoidedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var charge in charges)
        {
            var voided = charge.Void(CancelReason, now, userId);
            if (voided.IsFailure)
            {
                // Only an already-voided charge can fail here, and the query above excluded those.
                throw new InvalidOperationException(
                    $"Voiding a service charge while cancelling a check-in failed: {voided.Error.Code}.");
            }

            await ServiceChargeRefunder.RefundNetPaidAsync(db, charge, CancelReason, userId, now, cancellationToken);
        }
    }

    /// <summary>
    /// BUSINESS_RULES.md §7 <i>Sale at the desk</i>: each named sale, فروشگاه, آنالیز or متفرقه, is voided
    /// exactly as from its own void button, money going back the way it came. Every one must be a
    /// standing sale of this visit, or none is voided.
    /// </summary>
    /// <remarks>
    /// The kinds are spelled out rather than calling <see cref="ServiceCharge.IsSaleKind"/>: EF Core
    /// cannot turn a C# method into SQL, but it can turn this comparison into a <c>WHERE</c>.
    /// </remarks>
    private async Task<Result> VoidSalesAsync(
        Guid attendanceId, IReadOnlyList<Guid> saleIds, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (saleIds.Count == 0)
        {
            return Result.Success();
        }

        var sales = await db.ServiceCharges
            .Where(charge => saleIds.Contains(charge.Id)
                && charge.AttendanceId == attendanceId
                && (charge.Kind == ServiceChargeKind.Miscellaneous
                    || charge.Kind == ServiceChargeKind.Analysis
                    || charge.Kind == ServiceChargeKind.Other)
                && charge.VoidedAt == null)
            .ToListAsync(cancellationToken);

        // Another visit's sale, a هوازی id, an unknown id, or one voided since the box opened.
        if (sales.Count != saleIds.Count)
        {
            return Result.Failure(AttendanceErrors.SaleNotOnVisit);
        }

        foreach (var sale in sales)
        {
            var voided = sale.Void(CancelReason, now, userId);
            if (voided.IsFailure)
            {
                // The query excluded voided sales and the reason is a valid constant.
                throw new InvalidOperationException(
                    $"Voiding a sale while cancelling a check-in failed: {voided.Error.Code}.");
            }

            await ServiceChargeRefunder.RefundNetPaidAsync(db, sale, CancelReason, userId, now, cancellationToken);
        }

        return Result.Success();
    }

    /// <summary>
    /// BUSINESS_RULES.md §8: each named order is cancelled exactly as from the till. Every one must
    /// be a standing order of this visit, or none is cancelled.
    /// </summary>
    private async Task<Result> CancelCafeOrdersAsync(
        Guid attendanceId, IReadOnlyList<Guid> orderIds, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
        {
            return Result.Success();
        }

        var orders = await db.CafeOrders
            .Where(order => orderIds.Contains(order.Id)
                && order.AttendanceId == attendanceId
                && order.CancelledAt == null)
            .ToListAsync(cancellationToken);

        // Another visit's order, an unknown id, or one cancelled at the till since the box opened.
        if (orders.Count != orderIds.Count)
        {
            return Result.Failure(AttendanceErrors.CafeOrderNotOnVisit);
        }

        foreach (var order in orders)
        {
            var orderCancelled = order.Cancel(CancelReason, now, userId);
            if (orderCancelled.IsFailure)
            {
                // The query excluded cancelled orders and the reason is a valid constant.
                throw new InvalidOperationException(
                    $"Cancelling a cafe order while cancelling a check-in failed: {orderCancelled.Error.Code}.");
            }

            await CafeOrderRefunder.RefundNetPaidAsync(db, order, userId, now, cancellationToken);
        }

        return Result.Success();
    }
}
