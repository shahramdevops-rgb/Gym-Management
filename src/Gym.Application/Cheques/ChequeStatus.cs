using System.Text.Json.Serialization;

using Gym.Domain.Cheques;

namespace Gym.Application.Cheques;

/// <summary>
/// Where a cheque stands (BUSINESS_RULES.md §9 <i>Cheques</i>): read from its fields, never stored.
/// </summary>
/// <remarks>
/// The converter sits on the type because the list's query string takes it too: without it the
/// OpenAPI document would describe the filter as a number, while the query string takes its name.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<ChequeStatus>))]
public enum ChequeStatus
{
    /// <summary>«در انتظار»: neither passed nor cancelled, past its date or not.</summary>
    Pending,

    /// <summary>«پاس شد»: the Owner marked it once the money left the account.</summary>
    Passed,

    /// <summary>«باطل شده»: entered by mistake or taken back from the payee.</summary>
    Cancelled,
}

public static class ChequeStatusExtensions
{
    public static ChequeStatus GetStatus(this Cheque cheque)
    {
        ArgumentNullException.ThrowIfNull(cheque);

        if (cheque.IsPassed)
        {
            return ChequeStatus.Passed;
        }

        return cheque.IsCancelled ? ChequeStatus.Cancelled : ChequeStatus.Pending;
    }
}
