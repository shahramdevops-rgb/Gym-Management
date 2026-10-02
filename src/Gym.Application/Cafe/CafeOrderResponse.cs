using System.Text.Json.Serialization;

using Gym.Domain.Cafe;
using Gym.Domain.Payments;

namespace Gym.Application.Cafe;

/// <param name="MemberId"><c>null</c> for a walk-in customer (BUSINESS_RULES.md §8).</param>
/// <param name="AttendanceId">The visit it was bought during, or <c>null</c> for a buyer who was not inside.</param>
/// <param name="GuestName">
/// The guest's name when the order is on a guest's visit (BUSINESS_RULES.md §7 <i>Guest visit</i>):
/// no member, and no walk-in either, because it may be unpaid. <c>null</c> otherwise.
/// </param>
/// <param name="NetPaid">Payments less refunds, calculated — there is no paid column.</param>
/// <param name="PaymentStatus">
/// Unpaid, Partial or Paid, worked out from <see cref="TotalAmount"/> and <see cref="NetPaid"/>
/// the same way a subscription's is.
/// </param>
/// <param name="Outstanding">
/// What is still owed on this order, which is what reaches the member's debt. Zero once the order
/// is cancelled, however much was never paid.
/// </param>
public sealed record CafeOrderResponse(
    Guid Id,
    Guid? MemberId,
    string? MemberFullName,
    string? GuestName,
    Guid? AttendanceId,
    decimal TotalAmount,
    DateOnly OrderedOn,
    Guid PlacedByUserId,
    DateTimeOffset? CancelledAt,
    string? CancelReason,
    decimal NetPaid,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentStatus>))] PaymentStatus PaymentStatus,
    decimal Outstanding,
    uint Version,
    DateTimeOffset CreatedAt,
    IReadOnlyList<CafeOrderItemResponse> Items)
{
    public static CafeOrderResponse From(CafeOrder order, string? memberFullName, decimal netPaid, string? guestName = null)
    {
        ArgumentNullException.ThrowIfNull(order);

        return new CafeOrderResponse(
            order.Id,
            order.MemberId,
            memberFullName,
            guestName,
            order.AttendanceId,
            order.TotalAmount,
            order.OrderedOn,
            order.PlacedByUserId,
            order.CancelledAt,
            order.CancelReason,
            netPaid,
            PaymentStatusCalculator.Calculate(order.TotalAmount, netPaid),
            order.Outstanding(netPaid),
            order.Version,
            order.CreatedAt,
            order.Items.Select(CafeOrderItemResponse.From).ToList());
    }
}

/// <param name="ProductName">The snapshot taken at the till, not the product's name today.</param>
/// <param name="UnitPrice">The snapshot taken at the till, not the product's price today.</param>
public sealed record CafeOrderItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal)
{
    public static CafeOrderItemResponse From(CafeOrderItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new CafeOrderItemResponse(
            item.Id,
            item.ProductId,
            item.ProductName,
            item.UnitPrice,
            item.Quantity,
            item.LineTotal);
    }
}
