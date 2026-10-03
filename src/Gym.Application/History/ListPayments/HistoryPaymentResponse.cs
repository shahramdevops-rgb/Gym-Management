using System.Text.Json.Serialization;

using Gym.Application.Payments;
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
/// Whose item it was. <c>null</c> for a cafe order of a walk-in or of a guest, neither of which has
/// a member (§8).
/// </param>
/// <param name="GuestName">
/// For a guest's cafe order, the name typed at the desk for their visit; otherwise <c>null</c>.
/// </param>
/// <param name="SubscriptionPlan">What the subscription sold, which the frontend labels. <c>null</c> unless a subscription.</param>
/// <param name="ServiceKind"><c>null</c> unless a service charge.</param>
/// <param name="ServiceDescription">What a miscellaneous sale sold (§7); <c>null</c> for anything else.</param>
/// <param name="Kind">A payment, or a refund — marked on screen, with its <paramref name="Reason"/>.</param>
/// <param name="TargetUndone">
/// The item was cancelled (a subscription or cafe order) or voided (هوازی) after this money was
/// taken. The row stays, marked, so the refund that gave it back reads beside it.
/// </param>
/// <param name="ReceivedByFullName">Who took the money or gave it back.</param>
/// <param name="Settlement">
/// The «تسویه یکجا» this payment was one row of, so the screen can show its rows together;
/// <c>null</c> for money taken on its own and for every refund.
/// </param>
public sealed record HistoryPaymentResponse(
    Guid Id,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentTargetKind>))] PaymentTargetKind Source,
    Guid TargetId,
    Guid? MemberId,
    string? MemberFullName,
    string? GuestName,
    PlanSummary? SubscriptionPlan,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ServiceChargeKind>))] ServiceChargeKind? ServiceKind,
    string? ServiceDescription,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentKind>))] PaymentKind Kind,
    decimal Amount,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentMethod>))] PaymentMethod Method,
    string? ReferenceNumber,
    string? Reason,
    DateTimeOffset PaidAt,
    bool TargetUndone,
    string? ReceivedByFullName,
    SettlementSummary? Settlement);
