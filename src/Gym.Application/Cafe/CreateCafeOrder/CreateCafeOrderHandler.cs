using Gym.Application.Common;
using Gym.Application.Payments;
using Gym.Domain.Attendances;
using Gym.Domain.Cafe;
using Gym.Domain.Common;
using Gym.Domain.Members;
using Gym.Domain.Payments;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Cafe.CreateCafeOrder;

/// <summary>
/// Rings up a cafe order and, when money changes hands at the till, registers that payment in the
/// same transaction (BUSINESS_RULES.md §8). Front-desk work, both roles.
/// </summary>
/// <remarks>
/// <para>
/// Nothing is reserved and nothing is counted: the gym keeps no stock, so this can never be
/// refused for want of goods. What it does refuse is a product that is not sellable today — its
/// own switch or its category's is off — because the price list is what says whether something
/// can be bought (BUSINESS_RULES.md §8).
/// </para>
/// <para>
/// One transaction covers the order, its lines and the payment. A walk-in order must be paid in
/// full, an order on a member's account may be paid in part or not at all, and either way what is
/// left is that member's debt (§5 <i>Member debt</i>) — there is no paid column to keep correct.
/// </para>
/// </remarks>
public sealed class CreateCafeOrderHandler(
    IAppDbContext db, IGymCalendar calendar, TimeProvider time, ICurrentUser currentUser)
{
    public async Task<Result<CafeOrderResponse>> Handle(
        CreateCafeOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // The endpoint's policy requires an authenticated user, so this is a wiring bug if hit.
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Creating a cafe order was called without an authenticated user.");

        string? memberFullName = null;
        if (command.MemberId is { } memberId)
        {
            var member = await db.Members.AsNoTracking()
                .Where(m => m.Id == memberId)
                .Select(m => new { m.FullName })
                .SingleOrDefaultAsync(cancellationToken);
            if (member is null)
            {
                return Result.Failure<CafeOrderResponse>(MemberErrors.NotFound);
            }

            // An inactive member is not refused: they are not being let into the gym, they are
            // buying a bottle of water, and BUSINESS_RULES.md §2 only stops check-in and new
            // subscriptions. Money owed never blocks anything either (§0).
            memberFullName = member.FullName;
        }

        var visitId = command.AttendanceId;
        if (visitId is { } attendanceId)
        {
            var visit = await db.Attendances.AsNoTracking()
                .Where(a => a.Id == attendanceId)
                .Select(a => new { a.MemberId, a.CheckedOutAt })
                .SingleOrDefaultAsync(cancellationToken);
            if (visit is null)
            {
                return Result.Failure<CafeOrderResponse>(AttendanceErrors.NotFound);
            }

            // The member's own visit: a purchase put on the wrong visit would show up at the wrong
            // person's check-out, and a visit with no member named at all is the same mistake.
            if (visit.MemberId != command.MemberId)
            {
                return Result.Failure<CafeOrderResponse>(CafeOrderErrors.VisitOfAnotherMember);
            }

            // Only while they are inside, the same rule as a هوازی charge (BUSINESS_RULES.md §7).
            // CheckedOutAt covers all three ways a visit closes: check-out, the nightly job and a
            // cancelled check-in.
            if (visit.CheckedOutAt is not null)
            {
                return Result.Failure<CafeOrderResponse>(CafeOrderErrors.VisitNotOpen);
            }
        }
        else if (command.MemberId is { } buyerId)
        {
            // A member who is inside is buying during their visit wherever the order is rung up —
            // the till may be on another computer — so it joins that visit and shows on its locker
            // (BUSINESS_RULES.md §8). The partial unique index allows at most one open visit.
            visitId = await db.Attendances.AsNoTracking()
                .Where(a => a.MemberId == buyerId && a.CheckedOutAt == null)
                .Select(a => (Guid?)a.Id)
                .SingleOrDefaultAsync(cancellationToken);
        }

        var productIds = command.Items.Select(item => item.ProductId).Distinct().ToList();

        // Joined to the category, because "sellable" is a question about both rows.
        var products = await db.Products
            .Where(product => productIds.Contains(product.Id))
            .Join(
                db.ProductCategories,
                product => product.CategoryId,
                category => category.Id,
                (product, category) => new { Product = product, CategoryIsActive = category.IsActive })
            .ToListAsync(cancellationToken);

        var lines = new List<(Product Product, int Quantity)>(command.Items.Count);
        foreach (var item in command.Items)
        {
            var row = products.Find(candidate => candidate.Product.Id == item.ProductId);
            if (row is null)
            {
                return Result.Failure<CafeOrderResponse>(CafeOrderErrors.ProductNotFound);
            }

            var sellable = row.Product.EnsureCanBeSold();
            if (sellable.IsFailure || !row.CategoryIsActive)
            {
                // One answer for both switches: to the person at the till the item is ناموجود, and
                // which of the two flags said so is not their problem.
                return Result.Failure<CafeOrderResponse>(ProductErrors.Inactive);
            }

            lines.Add((row.Product, item.Quantity));
        }

        var created = CafeOrder.Create(command.MemberId, lines, calendar.Today(), userId, visitId);
        if (created.IsFailure)
        {
            return Result.Failure<CafeOrderResponse>(created.Error);
        }

        var order = created.Value;

        var paid = command.Payment?.Amount ?? 0m;
        if (paid > order.TotalAmount)
        {
            return Result.Failure<CafeOrderResponse>(CafeOrderErrors.PaidMoreThanTheOrder);
        }

        if (command.MemberId is null && paid != order.TotalAmount)
        {
            // BUSINESS_RULES.md §8: there is no account to leave a balance on.
            return Result.Failure<CafeOrderResponse>(CafeOrderErrors.WalkInMustBePaidInFull);
        }

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        db.CafeOrders.Add(order);

        if (command.Payment is { } payment && payment.Amount > 0)
        {
            var registered = Payment.RegisterForCafeOrder(
                order.Id, payment.Amount, payment.Method, payment.ReferenceNumber, userId, time.GetUtcNow());
            if (registered.IsFailure)
            {
                return Result.Failure<CafeOrderResponse>(registered.Error);
            }

            db.Payments.Add(registered.Value);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return CafeOrderResponse.From(order, memberFullName, paid);
    }
}
