using System.Text.Json.Serialization;

using Gym.Application.Cafe;
using Gym.Application.ServiceCharges;
using Gym.Application.Subscriptions;
using Gym.Domain.Payments;
using Gym.Domain.ServiceCharges;

namespace Gym.Application.Members.GetMemberDebt;

/// <param name="Total">
/// The sum of <paramref name="Items"/>. The front desk never sees it on its own: opening it shows
/// the breakdown, item by item (BUSINESS_RULES.md §5 <i>Member debt</i>).
/// </param>
/// <param name="Items">
/// Only what is still owed, newest first. A cancelled subscription, a voided service charge, and
/// anything free or fully paid, owes nothing and is not here.
/// </param>
public sealed record MemberDebtResponse(decimal Total, IReadOnlyList<MemberDebtItemResponse> Items);

/// <param name="Kind">What the money is owed for. Decides which of the two labels below is filled.</param>
/// <param name="Id">The subscription or service charge a payment for this item is posted against.</param>
/// <param name="Plan">
/// What the subscription sold, which the frontend turns into its label (BUSINESS_RULES.md §3).
/// <c>null</c> for a service charge and a cafe order.
/// </param>
/// <param name="ServiceKind"><c>null</c> for a subscription. The frontend turns it into Persian.</param>
/// <param name="Sale">What a sale (فروشگاه, آنالیز) sold (§7); <c>null</c> for everything else.</param>
/// <param name="EndDate"><c>null</c> for a service charge: it covers the one day it was charged on.</param>
/// <param name="Outstanding"><c>Price − NetPaid</c>: what the member still owes on this item.</param>
/// <param name="CafeItems">
/// What a cafe order bought, line by line with its price, so the desk can say what the cafe debt is
/// for. The same lines the member's cafe purchases show. Empty for anything that is not a cafe order.
/// </param>
public sealed record MemberDebtItemResponse(
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentTargetKind>))] PaymentTargetKind Kind,
    Guid Id,
    PlanSummary? Plan,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ServiceChargeKind>))] ServiceChargeKind? ServiceKind,
    SaleSummary? Sale,
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal Price,
    decimal NetPaid,
    decimal Outstanding,
    IReadOnlyList<CafeOrderItemResponse> CafeItems);
