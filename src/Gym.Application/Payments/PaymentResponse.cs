using System.Text.Json.Serialization;

using Gym.Domain.Payments;

namespace Gym.Application.Payments;

/// <param name="TargetNetPaid">
/// The net paid amount, after this payment, of the one thing it belongs to (§5) — the
/// subscription or the service charge.
/// </param>
/// <param name="TargetPaymentStatus">That same item's calculated status after this payment.</param>
public sealed record PaymentResponse(
    Guid Id,
    Guid? SubscriptionId,
    Guid? ServiceChargeId,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentKind>))] PaymentKind Kind,
    decimal Amount,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentMethod>))] PaymentMethod Method,
    string? ReferenceNumber,
    DateTimeOffset PaidAt,
    Guid ReceivedByUserId,
    string? Reason,
    decimal TargetNetPaid,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentStatus>))] PaymentStatus TargetPaymentStatus,
    DateTimeOffset CreatedAt)
{
    public static PaymentResponse From(Payment payment, decimal targetNetPaid, PaymentStatus targetPaymentStatus)
    {
        ArgumentNullException.ThrowIfNull(payment);

        return new PaymentResponse(
            payment.Id,
            payment.SubscriptionId,
            payment.ServiceChargeId,
            payment.Kind,
            payment.Amount,
            payment.Method,
            payment.ReferenceNumber,
            payment.PaidAt,
            payment.ReceivedByUserId,
            payment.Reason,
            targetNetPaid,
            targetPaymentStatus,
            payment.CreatedAt);
    }
}
