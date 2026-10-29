using System.Text.Json.Serialization;

using Gym.Application.Cafe;
using Gym.Domain.Payments;

namespace Gym.Application.SiteRecords.ListMyCafeOrders;

/// <summary>
/// One cafe purchase (BUSINESS_RULES.md §15 <i>The member's own records</i>): the panel's
/// <see cref="CafeOrderResponse"/> without who sold it and the reason written for a cancellation. A
/// cancelled purchase is marked by <see cref="CancelledAt"/> and owes nothing.
/// </summary>
/// <param name="CreatedAt">When it was bought, for the time beside <paramref name="OrderedOn"/>.</param>
public sealed record MyCafeOrderResponse(
    Guid Id,
    decimal TotalAmount,
    DateOnly OrderedOn,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CancelledAt,
    decimal NetPaid,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentStatus>))] PaymentStatus PaymentStatus,
    decimal Outstanding,
    IReadOnlyList<SiteCafeItemResponse> Items)
{
    public static MyCafeOrderResponse From(CafeOrderResponse order)
    {
        ArgumentNullException.ThrowIfNull(order);

        return new MyCafeOrderResponse(
            order.Id,
            order.TotalAmount,
            order.OrderedOn,
            order.CreatedAt,
            order.CancelledAt,
            order.NetPaid,
            order.PaymentStatus,
            order.Outstanding,
            order.Items.Select(SiteCafeItemResponse.From).ToList());
    }
}
