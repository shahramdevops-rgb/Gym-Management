using System.Text.Json.Serialization;

using Gym.Domain.Payments;
using Gym.Domain.ServiceCharges;

namespace Gym.Application.Payments;

/// <summary>
/// One row of a member's payment history (task 4.5), across everything they have paid for.
/// Deliberately lighter than <see cref="PaymentResponse"/>: <c>TargetNetPaid</c> and
/// <c>TargetPaymentStatus</c> only make sense as "the result of the payment just registered", not
/// for a list of historical rows spanning several items.
/// </summary>
/// <param name="TargetKind">What was paid for. Decides which of the two labels below is filled.</param>
/// <param name="TargetId">The subscription or the service charge this money went against.</param>
/// <param name="SubscriptionPlanName">
/// The plan's name now, not when it was sold (BUSINESS_RULES.md §4). <c>null</c> for a service charge.
/// </param>
/// <param name="ServiceKind"><c>null</c> for a subscription. The frontend turns it into Persian.</param>
public sealed record PaymentHistoryResponse(
    Guid Id,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentTargetKind>))] PaymentTargetKind TargetKind,
    Guid TargetId,
    string? SubscriptionPlanName,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ServiceChargeKind>))] ServiceChargeKind? ServiceKind,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentKind>))] PaymentKind Kind,
    decimal Amount,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentMethod>))] PaymentMethod Method,
    string? ReferenceNumber,
    DateTimeOffset PaidAt,
    Guid ReceivedByUserId,
    string? Reason,
    DateTimeOffset CreatedAt);
