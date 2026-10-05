using System.Text.Json.Serialization;

using Gym.Domain.Payables;

namespace Gym.Application.Payables;

/// <summary>
/// Where a cheque or instalment stands (BUSINESS_RULES.md §9 <i>Cheques and instalments</i>): read
/// from its fields, never stored.
/// </summary>
/// <remarks>
/// The converter sits on the type because the list's query string takes it too: without it the
/// OpenAPI document would describe the filter as a number, while the query string takes its name.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<PayableStatus>))]
public enum PayableStatus
{
    /// <summary>«در انتظار»: neither paid nor cancelled, past its date or not.</summary>
    Pending,

    /// <summary>«پاس شد» for a cheque, «پرداخت شد» for an instalment: its expense is recorded.</summary>
    Paid,

    /// <summary>«باطل شده»: entered by mistake or taken back from the payee.</summary>
    Cancelled,
}

public static class PayableStatusExtensions
{
    public static PayableStatus GetStatus(this Payable payable)
    {
        ArgumentNullException.ThrowIfNull(payable);

        if (payable.IsPaid)
        {
            return PayableStatus.Paid;
        }

        return payable.IsCancelled ? PayableStatus.Cancelled : PayableStatus.Pending;
    }
}
