using System.Text.Json.Serialization;

using Gym.Application.Subscriptions;
using Gym.Domain.Payments;
using Gym.Domain.ServiceCharges;

namespace Gym.Application.Payments;

/// <summary>
/// One row of a member's payment history (task 4.5), across everything they have paid for.
/// Deliberately lighter than <see cref="PaymentResponse"/>: <c>TargetNetPaid</c> and
/// <c>TargetPaymentStatus</c> only make sense as "the result of the payment just registered", not
/// for a list of historical rows spanning several items.
/// </summary>
/// <param name="TargetKind">
/// What was paid for. Decides which of the two labels below is filled; a cafe order has neither.
/// </param>
/// <param name="TargetId">The subscription, service charge or cafe order this money went against.</param>
/// <param name="SubscriptionPlan">
/// What the subscription sold, which the frontend labels (BUSINESS_RULES.md §3). <c>null</c> unless a subscription.
/// </param>
/// <param name="ServiceKind">
/// <c>null</c> unless a service charge. The frontend turns it into Persian.
/// </param>
/// <param name="ServiceDescription">What a sale (فروشگاه, آنالیز) sold (§7); <c>null</c> for anything else.</param>
/// <param name="Settlement">
/// The «تسویه یکجا» this payment was one row of; <c>null</c> for money taken on its own and for
/// every refund.
/// </param>
public sealed record PaymentHistoryResponse(
    Guid Id,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentTargetKind>))] PaymentTargetKind TargetKind,
    Guid TargetId,
    PlanSummary? SubscriptionPlan,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ServiceChargeKind>))] ServiceChargeKind? ServiceKind,
    string? ServiceDescription,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentKind>))] PaymentKind Kind,
    decimal Amount,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentMethod>))] PaymentMethod Method,
    string? ReferenceNumber,
    DateTimeOffset PaidAt,
    Guid ReceivedByUserId,
    string? Reason,
    DateTimeOffset CreatedAt,
    SettlementSummary? Settlement);
