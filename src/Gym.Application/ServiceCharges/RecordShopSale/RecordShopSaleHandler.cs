using Gym.Application.Common;
using Gym.Domain.Attendances;
using Gym.Domain.Common;
using Gym.Domain.ServiceCharges;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.ServiceCharges.RecordShopSale;

/// <summary>
/// Sells one or more «فروشگاه» items during an open visit (BUSINESS_RULES.md §7 <i>Sale at the
/// desk</i>), each its own charge on the member's account. Front desk work, so both roles.
/// </summary>
/// <remarks>
/// No money is taken here (decided with the developer, 1405/07/12): the items become debt, paid
/// afterwards from the tile's list or in «تسویه یکجا». All the items are saved together, so the
/// desk never sees half a sale: one item the domain refuses refuses them all.
/// </remarks>
public sealed class RecordShopSaleHandler(
    IAppDbContext db, IGymCalendar calendar, ICurrentUser currentUser)
{
    public async Task<Result<IReadOnlyList<ServiceChargeResponse>>> Handle(
        Guid attendanceId, RecordShopSaleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var attendance = await db.Attendances.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == attendanceId, cancellationToken);
        if (attendance is null)
        {
            return Result.Failure<IReadOnlyList<ServiceChargeResponse>>(AttendanceErrors.NotFound);
        }

        // Only an open visit, the same as هوازی: a closed visit is history.
        if (attendance.CheckedOutAt is not null)
        {
            return Result.Failure<IReadOnlyList<ServiceChargeResponse>>(ServiceChargeErrors.VisitNotOpen);
        }

        // Members only (decided with the developer, 1405/07/11): a guest has no account to put it on.
        if (attendance.MemberId is not { } memberId)
        {
            return Result.Failure<IReadOnlyList<ServiceChargeResponse>>(ServiceChargeErrors.GuestVisit);
        }

        // The endpoint's policy requires an authenticated user, so this is a wiring bug if hit.
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Recording a shop sale was called without an authenticated user.");

        var today = calendar.Today();
        var charges = new List<ServiceCharge>();
        foreach (var item in command.Items)
        {
            var recorded = ServiceCharge.RecordShopItem(
                memberId, attendanceId, item.Description, item.Quantity, item.UnitPrice, today, userId);
            if (recorded.IsFailure)
            {
                return Result.Failure<IReadOnlyList<ServiceChargeResponse>>(recorded.Error);
            }

            charges.Add(recorded.Value);
        }

        db.ServiceCharges.AddRange(charges);
        await db.SaveChangesAsync(cancellationToken);

        // Explicit, because C# applies no implicit conversion to an interface type like IReadOnlyList.
        return Result.Success<IReadOnlyList<ServiceChargeResponse>>(charges
            .Select(charge => ServiceChargeResponse.From(charge, netPaid: 0, visitIsOpen: true))
            .ToList());
    }
}
