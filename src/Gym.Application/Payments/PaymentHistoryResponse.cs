using System.Text.Json.Serialization;

using Gym.Domain.Payments;

namespace Gym.Application.Payments;

/// <summary>
/// One row of a member's payment history (task 4.5). Deliberately lighter than
/// <see cref="PaymentResponse"/>: <c>SubscriptionNetPaid</c>/<c>SubscriptionPaymentStatus</c> only
/// make sense as "the result of the payment just registered", not for a list of historical rows
/// that can span several of the member's subscriptions.
/// </summary>
public sealed record PaymentHistoryResponse(
    Guid Id,
    Guid SubscriptionId,
    string SubscriptionPlanName,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentKind>))] PaymentKind Kind,
    decimal Amount,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentMethod>))] PaymentMethod Method,
    string? ReferenceNumber,
    DateTimeOffset PaidAt,
    Guid ReceivedByUserId,
    string? Reason,
    DateTimeOffset CreatedAt);
