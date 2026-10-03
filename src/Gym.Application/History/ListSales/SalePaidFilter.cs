using System.Text.Json.Serialization;

namespace Gym.Application.History.ListSales;

/// <summary>
/// The «پرداخت شده / پرداخت نشده» choice (BUSINESS_RULES.md §12 <i>Sales in the history</i>). Not
/// <c>PaymentStatus</c>: a partly paid sale is unpaid here, because it still owes money. Cancelled and
/// voided sales match neither.
/// </summary>
/// <remarks>
/// The converter sits on the type because no response carries this enum: without it the OpenAPI
/// document would describe it as a number, while the query string takes its name.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<SalePaidFilter>))]
public enum SalePaidFilter
{
    /// <summary>Net paid covers the amount; a free plan included.</summary>
    Paid,

    /// <summary>Anything still owed, a partial payment included.</summary>
    Unpaid,
}
