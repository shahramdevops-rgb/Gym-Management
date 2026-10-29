using System.Text.Json.Serialization;

using Gym.Application.Payments;
using Gym.Application.Subscriptions;
using Gym.Domain.Payments;
using Gym.Domain.ServiceCharges;

namespace Gym.Application.SiteRecords.ListMyPayments;

/// <summary>
/// One payment or refund (BUSINESS_RULES.md §15 <i>The member's own records</i>): the panel's
/// <see cref="PaymentHistoryResponse"/> without who took the money and the reason written for a
/// refund. A refund is marked by <see cref="Kind"/>.
/// </summary>
/// <param name="TargetId">The plan, charge or cafe purchase the money went against.</param>
/// <param name="SubscriptionPlan">What a plan sold; <c>null</c> unless a plan.</param>
/// <param name="ServiceKind"><c>null</c> unless a service charge.</param>
/// <param name="ServiceDescription">What a sale (فروشگاه, آنالیز) sold; <c>null</c> for anything else.</param>
/// <param name="ReferenceNumber">The card reader's reference, which the member may hold a receipt for.</param>
/// <param name="Settlement">The «تسویه یکجا» this payment was one row of, or <c>null</c>.</param>
public sealed record MyPaymentResponse(
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
    SettlementSummary? Settlement)
{
    public static MyPaymentResponse From(PaymentHistoryResponse payment)
    {
        ArgumentNullException.ThrowIfNull(payment);

        return new MyPaymentResponse(
            payment.Id,
            payment.TargetKind,
            payment.TargetId,
            payment.SubscriptionPlan,
            payment.ServiceKind,
            payment.ServiceDescription,
            payment.Kind,
            payment.Amount,
            payment.Method,
            payment.ReferenceNumber,
            payment.PaidAt,
            payment.Settlement);
    }
}
