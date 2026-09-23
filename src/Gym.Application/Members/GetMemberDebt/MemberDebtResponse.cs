using System.Text.Json.Serialization;

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
/// <param name="PlanName">
/// The plan's name <i>now</i>, like every other place a sold subscription is shown
/// (BUSINESS_RULES.md §4). <c>null</c> for a service charge.
/// </param>
/// <param name="ServiceKind"><c>null</c> for a subscription. The frontend turns it into Persian.</param>
/// <param name="EndDate"><c>null</c> for a service charge: it covers the one day it was charged on.</param>
/// <param name="Outstanding"><c>Price − NetPaid</c>: what the member still owes on this item.</param>
public sealed record MemberDebtItemResponse(
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentTargetKind>))] PaymentTargetKind Kind,
    Guid Id,
    string? PlanName,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ServiceChargeKind>))] ServiceChargeKind? ServiceKind,
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal Price,
    decimal NetPaid,
    decimal Outstanding);
