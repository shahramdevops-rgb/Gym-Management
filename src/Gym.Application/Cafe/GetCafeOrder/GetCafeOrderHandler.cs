using Gym.Application.Common;
using Gym.Application.Payments;
using Gym.Domain.Cafe;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Cafe.GetCafeOrder;

/// <summary>One order with its lines, what has been paid on it, and what is still owed.</summary>
public sealed class GetCafeOrderHandler(IAppDbContext db)
{
    public async Task<Result<CafeOrderResponse>> Handle(Guid id, CancellationToken cancellationToken)
    {
        var order = await db.CafeOrders
            .AsNoTracking()
            .Include(o => o.Items)
            .SingleOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (order is null)
        {
            return Result.Failure<CafeOrderResponse>(CafeOrderErrors.NotFound);
        }

        var memberFullName = order.MemberId is { } memberId
            ? await db.Members.AsNoTracking()
                .Where(member => member.Id == memberId)
                .Select(member => member.FullName)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        var netPaid = await PaymentLedger.GetNetPaidForCafeOrderAsync(db, id, cancellationToken);

        var guestName = await CafeOrderGuests.NameAsync(db, order, cancellationToken);

        return CafeOrderResponse.From(order, memberFullName, netPaid, guestName);
    }
}
