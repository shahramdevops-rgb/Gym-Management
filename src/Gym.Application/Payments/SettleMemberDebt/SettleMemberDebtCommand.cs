using System.Text.Json.Serialization;

using Gym.Domain.Payments;

namespace Gym.Application.Payments.SettleMemberDebt;

/// <param name="Amount">What the member handed over, spread over <paramref name="Items"/>.</param>
/// <param name="Items">The items the desk ticked, each with the figure it was shown as owed.</param>
public sealed record SettleMemberDebtCommand(
    decimal Amount,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentMethod>))] PaymentMethod Method,
    string? ReferenceNumber,
    IReadOnlyList<SettleMemberDebtItem> Items);

/// <param name="Id">The subscription's, service charge's or cafe order's id, from the member's debt.</param>
/// <param name="Outstanding">
/// What the desk was shown as still owed on this item. The settlement goes through only if that is
/// still true (BUSINESS_RULES.md §5: "what the desk saw is what gets paid").
/// </param>
public sealed record SettleMemberDebtItem(
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentTargetKind>))] PaymentTargetKind Kind,
    Guid Id,
    decimal Outstanding);
