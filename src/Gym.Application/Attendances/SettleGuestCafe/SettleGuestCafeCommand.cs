using System.Text.Json.Serialization;

using Gym.Domain.Payments;

namespace Gym.Application.Attendances.SettleGuestCafe;

/// <param name="Amount">
/// What the guest handed over: everything the visit's cafe orders still owe, as the box showed it.
/// It must still be exactly that, or nothing is paid (BUSINESS_RULES.md §5: "what the desk saw is
/// what gets paid"). An order rung up at the till while the box was open changes the total.
/// </param>
public sealed record SettleGuestCafeCommand(
    decimal Amount,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentMethod>))] PaymentMethod Method,
    string? ReferenceNumber);
