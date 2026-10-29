using System.Text.Json.Serialization;

using Gym.Application.Members.GetMemberDebt;
using Gym.Application.ServiceCharges;
using Gym.Application.Subscriptions;
using Gym.Domain.Payments;
using Gym.Domain.ServiceCharges;

namespace Gym.Application.SiteRecords.GetMyDebt;

/// <summary>
/// What the member still owes, item by item, as the desk sees it (BUSINESS_RULES.md §5 <i>Member
/// debt</i>, §15). The panel's breakdown has no staff fields; it is copied so the site's shape does
/// not change when the panel's does.
/// </summary>
public sealed record MyDebtResponse(decimal Total, IReadOnlyList<MyDebtItemResponse> Items)
{
    public static MyDebtResponse From(MemberDebtResponse debt)
    {
        ArgumentNullException.ThrowIfNull(debt);

        return new MyDebtResponse(debt.Total, debt.Items.Select(MyDebtItemResponse.From).ToList());
    }
}

/// <param name="Id">The plan's, charge's or purchase's id, the same one its row in the history has.</param>
/// <param name="Plan">What a plan sold; <c>null</c> for anything else.</param>
/// <param name="ServiceKind"><c>null</c> for anything that is not a service charge.</param>
/// <param name="Sale">What a sale (فروشگاه, آنالیز) sold; <c>null</c> for everything else.</param>
/// <param name="EndDate"><c>null</c> for a service charge and a cafe purchase: each covers one day.</param>
/// <param name="CafeItems">What a cafe purchase bought; empty for anything else.</param>
public sealed record MyDebtItemResponse(
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
    IReadOnlyList<SiteCafeItemResponse> CafeItems)
{
    public static MyDebtItemResponse From(MemberDebtItemResponse item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new MyDebtItemResponse(
            item.Kind,
            item.Id,
            item.Plan,
            item.ServiceKind,
            item.Sale,
            item.StartDate,
            item.EndDate,
            item.Price,
            item.NetPaid,
            item.Outstanding,
            item.CafeItems.Select(SiteCafeItemResponse.From).ToList());
    }
}
