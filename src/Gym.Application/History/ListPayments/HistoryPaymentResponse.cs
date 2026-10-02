using System.Text.Json.Serialization;

using Gym.Application.Subscriptions;
using Gym.Domain.Payments;
using Gym.Domain.ServiceCharges;

namespace Gym.Application.History.ListPayments;

/// <summary>
/// One payment or refund in the gym's history (BUSINESS_RULES.md §12 <i>History</i>). The member's
/// own payment history row (<c>PaymentHistoryResponse</c>) plus whose money it was, who took it and
/// whether its item has since been undone.
/// </summary>
/// <param name="Source">What the money was for: a subscription, هوازی or the cafe.</param>
/// <param name="TargetId">The subscription, service charge or cafe order this money went against.</param>
/// <param name="MemberId">
/// Whose item it was. <c>null</c> for a walk-in's cafe order, which has no member (§8).
/// </param>
/// <param name="SubscriptionPlan">What the subscription sold, which the frontend labels. <c>null</c> unless a subscription.</param>
/// <param name="ServiceKind"><c>null</c> unless a service charge.</param>
/// <param name="Kind">A payment, or a refund — marked on screen, with its <paramref name="Reason"/>.</param>
/// <param name="TargetUndone">
/// The item was cancelled (a subscription or cafe order) or voided (هوازی) after this money was
/// taken. The row stays, marked, so the refund that gave it back reads beside it.
/// </param>
/// <param name="ReceivedByFullName">Who took the money or gave it back.</param>
public sealed record HistoryPaymentResponse(
    Guid Id,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentTargetKind>))] PaymentTargetKind Source,
    Guid TargetId,
    Guid? MemberId,
    string? MemberFullName,
    PlanSummary? SubscriptionPlan,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ServiceChargeKind>))] ServiceChargeKind? ServiceKind,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentKind>))] PaymentKind Kind,
    decimal Amount,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentMethod>))] PaymentMethod Method,
    string? ReferenceNumber,
    string? Reason,
    DateTimeOffset PaidAt,
    bool TargetUndone,
    string? ReceivedByFullName);
