using Gym.Application.Common;
using Gym.Domain.Cafe;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Cafe;

/// <summary>
/// Whose name an order on a guest's visit goes under (BUSINESS_RULES.md §7 <i>Guest visit</i>, §8).
/// The order names no member, so the name comes from its visit. One query for a whole page.
/// </summary>
public static class CafeOrderGuests
{
    /// <summary>The guest's name for each guest's visit among <paramref name="orders"/>, keyed by visit.</summary>
    public static async Task<Dictionary<Guid, string>> NamesByVisitAsync(
        IAppDbContext db, IEnumerable<CafeOrder> orders, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(orders);

        // Only orders with a visit and no member can be a guest's; a member's never needs the lookup.
        var visitIds = orders
            .Where(order => order.MemberId is null && order.AttendanceId is not null)
            .Select(order => order.AttendanceId!.Value)
            .Distinct()
            .ToList();

        if (visitIds.Count == 0)
        {
            return [];
        }

        return await db.Attendances.AsNoTracking()
            .Where(a => visitIds.Contains(a.Id) && a.GuestName != null)
            .ToDictionaryAsync(a => a.Id, a => a.GuestName!, cancellationToken);
    }

    /// <summary>The guest's name for one order, or <c>null</c> when it is not on a guest's visit.</summary>
    public static async Task<string?> NameAsync(IAppDbContext db, CafeOrder order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);

        var names = await NamesByVisitAsync(db, [order], cancellationToken);

        return order.AttendanceId is { } visitId ? names.GetValueOrDefault(visitId) : null;
    }

    /// <summary>The name out of <see cref="NamesByVisitAsync"/> for one order of the page.</summary>
    public static string? For(Dictionary<Guid, string> namesByVisit, CafeOrder order)
    {
        ArgumentNullException.ThrowIfNull(namesByVisit);
        ArgumentNullException.ThrowIfNull(order);

        return order.AttendanceId is { } visitId ? namesByVisit.GetValueOrDefault(visitId) : null;
    }
}
