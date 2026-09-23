using System.Text.Json.Serialization;

using Gym.Domain.Payments;
using Gym.Domain.ServiceCharges;

namespace Gym.Application.ServiceCharges;

/// <summary>
/// One service charge as the front desk sees it (BUSINESS_RULES.md §7 <i>Gym services</i>).
/// </summary>
/// <param name="NetPaid">Payments minus refunds against this charge, so the screen can show what is left.</param>
/// <param name="PaymentStatus">
/// Calculated the same way a subscription's is (<see cref="PaymentStatusCalculator"/>): a charge
/// is one more thing that can be unpaid, partly paid or settled.
/// </param>
/// <param name="CanChangeAmount">
/// Whether the amount can still be corrected in place rather than voided and re-entered: the
/// visit is open, nothing has been paid against it, and it is not already voided. The screen
/// shows only the actions that are possible, the way the subscription history row does (task 4.7).
/// </param>
public sealed record ServiceChargeResponse(
    Guid Id,
    Guid MemberId,
    Guid AttendanceId,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ServiceChargeKind>))] ServiceChargeKind Kind,
    decimal Amount,
    DateOnly ChargedOn,
    Guid RecordedByUserId,
    DateTimeOffset? VoidedAt,
    string? VoidReason,
    decimal NetPaid,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentStatus>))] PaymentStatus PaymentStatus,
    bool CanChangeAmount,
    DateTimeOffset CreatedAt)
{
    /// <param name="visitIsOpen">
    /// Whether the attendance this charge belongs to is still open. It lives on another row, so
    /// the caller — which loaded that row to decide whether the charge was allowed at all —
    /// passes it in rather than this mapping fetching it again.
    /// </param>
    public static ServiceChargeResponse From(ServiceCharge charge, decimal netPaid, bool visitIsOpen)
    {
        ArgumentNullException.ThrowIfNull(charge);

        return new ServiceChargeResponse(
            charge.Id,
            charge.MemberId,
            charge.AttendanceId,
            charge.Kind,
            charge.Amount,
            charge.ChargedOn,
            charge.RecordedByUserId,
            charge.VoidedAt,
            charge.VoidReason,
            netPaid,
            PaymentStatusCalculator.Calculate(charge.Amount, netPaid),
            visitIsOpen && !charge.IsVoided && netPaid == 0,
            charge.CreatedAt);
    }
}
