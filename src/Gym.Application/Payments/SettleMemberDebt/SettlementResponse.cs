using System.Text.Json.Serialization;

using Gym.Domain.Payments;

namespace Gym.Application.Payments.SettleMemberDebt;

/// <param name="Payments">
/// One per item that received money, in the order the money was spent (cafe, هوازی, subscription).
/// An item the amount did not reach is not here.
/// </param>
/// <param name="RemainingDebt">
/// Everything the member still owes afterwards, unticked items included, so the success step can
/// say whether the account is clear.
/// </param>
public sealed record SettlementResponse(
    decimal Amount,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentMethod>))] PaymentMethod Method,
    IReadOnlyList<SettlementPaymentResponse> Payments,
    decimal RemainingDebt);

/// <param name="TargetId">The subscription, service charge or cafe order the payment belongs to.</param>
/// <param name="Outstanding">What is still owed on that item after this payment.</param>
public sealed record SettlementPaymentResponse(
    Guid PaymentId,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentTargetKind>))] PaymentTargetKind Kind,
    Guid TargetId,
    decimal Amount,
    decimal Outstanding);
