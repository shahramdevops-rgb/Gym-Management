using System.Text.Json.Serialization;

using Gym.Domain.Payments;

namespace Gym.Application.Payments;

/// <param name="SubscriptionNetPaid">The subscription's net paid amount after this payment.</param>
/// <param name="SubscriptionPaymentStatus">The subscription's calculated status after this payment.</param>
public sealed record PaymentResponse(
    Guid Id,
    Guid? SubscriptionId,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentKind>))] PaymentKind Kind,
    decimal Amount,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentMethod>))] PaymentMethod Method,
    string? ReferenceNumber,
    DateTimeOffset PaidAt,
    Guid ReceivedByUserId,
    string? Reason,
    decimal SubscriptionNetPaid,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentStatus>))] PaymentStatus SubscriptionPaymentStatus,
    DateTimeOffset CreatedAt)
{
    public static PaymentResponse From(Payment payment, decimal subscriptionNetPaid, PaymentStatus subscriptionPaymentStatus)
    {
        ArgumentNullException.ThrowIfNull(payment);

        return new PaymentResponse(
            payment.Id,
            payment.SubscriptionId,
            payment.Kind,
            payment.Amount,
            payment.Method,
            payment.ReferenceNumber,
            payment.PaidAt,
            payment.ReceivedByUserId,
            payment.Reason,
            subscriptionNetPaid,
            subscriptionPaymentStatus,
            payment.CreatedAt);
    }
}
