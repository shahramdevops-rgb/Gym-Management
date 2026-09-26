using System.Text.Json.Serialization;

using Gym.Domain.Payments;

namespace Gym.Application.Cafe.CreateCafeOrder;

/// <param name="MemberId">
/// <c>null</c> for a walk-in customer. Naming a member is exactly the act of putting the purchase
/// on their account so the money can be collected later (BUSINESS_RULES.md §8).
/// </param>
/// <param name="Items">What was bought, one line per product.</param>
/// <param name="Payment">
/// Money handed over at the till, or <c>null</c> for an order left entirely on a member's account.
/// It may be less than the total: whatever is left stays on the account (§5, instalments). A
/// walk-in order must carry a payment for the whole total, because there is no account to leave a
/// balance on.
/// </param>
/// <param name="AttendanceId">
/// The open visit this was bought during, when it is rung up from the "currently inside" board;
/// <c>null</c> from the till. It must be <paramref name="MemberId"/>'s own visit, and still open,
/// like a هوازی charge (BUSINESS_RULES.md §8).
/// </param>
public sealed record CreateCafeOrderCommand(
    Guid? MemberId,
    IReadOnlyList<CafeOrderLine> Items,
    CafeOrderPayment? Payment,
    Guid? AttendanceId = null);

public sealed record CafeOrderLine(Guid ProductId, int Quantity);

public sealed record CafeOrderPayment(
    decimal Amount,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentMethod>))] PaymentMethod Method,
    string? ReferenceNumber);
