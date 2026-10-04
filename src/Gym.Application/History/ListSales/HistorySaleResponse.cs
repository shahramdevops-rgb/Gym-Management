using System.Text.Json.Serialization;

using Gym.Application.Subscriptions;
using Gym.Domain.Payments;

namespace Gym.Application.History.ListSales;

/// <summary>
/// One sale in the gym's history (BUSINESS_RULES.md §12 <i>Sales in the history</i>): a plan, a
/// هوازی, a فروشگاه item, an آنالیز or a cafe order, with what has been paid on it.
/// </summary>
/// <param name="Source">What kind of sale it is.</param>
/// <param name="Id">The subscription, service charge or cafe order.</param>
/// <param name="MemberId">
/// Whose sale. <c>null</c> for a cafe order of a walk-in, and for a guest's cafe order or charge
/// (§7 <i>Guest visit</i>, §8).
/// </param>
/// <param name="GuestName">
/// For a guest's cafe order or charge, the name typed for their visit; otherwise <c>null</c>.
/// </param>
/// <param name="Plan">What a subscription sold, which the frontend labels. <c>null</c> unless a subscription.</param>
/// <param name="Description">What a فروشگاه item was; <c>null</c> for anything else.</param>
/// <param name="Quantity">How many of a فروشگاه item; <c>null</c> for anything else.</param>
/// <param name="CafeItems">What a cafe order held; <c>null</c> unless a cafe order.</param>
/// <param name="Amount">What it was sold for.</param>
/// <param name="SoldAt">The moment it was recorded, for the time on screen.</param>
/// <param name="RecordedByFullName">
/// Who placed the cafe order or recorded the charge. Always <c>null</c> for a subscription: who sold
/// it is not shown (§4).
/// </param>
/// <param name="UndoneAt">When it was cancelled (a subscription or cafe order) or voided (a charge).</param>
/// <param name="UndoReason">Why it was cancelled or voided.</param>
/// <param name="NetPaid">Payments minus refunds against it.</param>
public sealed record HistorySaleResponse(
    [property: JsonConverter(typeof(JsonStringEnumConverter<SaleSource>))] SaleSource Source,
    Guid Id,
    Guid? MemberId,
    string? MemberFullName,
    string? GuestName,
    PlanSummary? Plan,
    string? Description,
    int? Quantity,
    IReadOnlyList<HistorySaleCafeItem>? CafeItems,
    decimal Amount,
    DateTimeOffset SoldAt,
    string? RecordedByFullName,
    DateTimeOffset? UndoneAt,
    string? UndoReason,
    decimal NetPaid,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentStatus>))] PaymentStatus PaymentStatus);

/// <summary>One line of a cafe order in the sales history: the product's name as sold, and how many.</summary>
public sealed record HistorySaleCafeItem(string ProductName, int Quantity);
