using System.Text.Json.Serialization;

using Gym.Domain.Payments;

namespace Gym.Application.ServiceCharges.RecordMiscellaneousSale;

/// <summary>
/// A sale the desk names itself (BUSINESS_RULES.md §7 <i>Miscellaneous sale</i>).
/// </summary>
/// <param name="Description">What was sold, as the desk calls it.</param>
/// <param name="Quantity">How many, 1 to 999.</param>
/// <param name="UnitPrice">The price of one; the system never checks it against anything.</param>
/// <param name="Method">
/// How the whole amount was paid there and then, or <c>null</c> to leave it on the member's
/// account («به حساب عضو»), where it counts toward their debt like the visit's cafe purchases.
/// </param>
public sealed record RecordMiscellaneousSaleCommand(
    string Description,
    int Quantity,
    decimal UnitPrice,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentMethod>))] PaymentMethod? Method);
