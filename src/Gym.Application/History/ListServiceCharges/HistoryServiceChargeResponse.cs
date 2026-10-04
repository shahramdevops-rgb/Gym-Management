using System.Text.Json.Serialization;

using Gym.Domain.Payments;
using Gym.Domain.ServiceCharges;

namespace Gym.Application.History.ListServiceCharges;

/// <summary>
/// One هوازی charge or sale (فروشگاه, آنالیز) in the gym's history (BUSINESS_RULES.md §12 <i>History</i>).
/// </summary>
/// <param name="MemberId">Whose charge; <c>null</c> on a guest's visit (§7 <i>Guest visit</i>).</param>
/// <param name="GuestName">For a guest's charge, the name typed for their visit; otherwise <c>null</c>.</param>
/// <param name="Description">What a sale (فروشگاه, آنالیز) sold; <c>null</c> for هوازی, as are the next two.</param>
/// <param name="ChargedOn">The business date of the visit, which the date range filters by.</param>
/// <param name="CreatedAt">The moment it was recorded, for the time of day on screen.</param>
/// <param name="RecordedByFullName">Who recorded it at the desk.</param>
/// <param name="VoidedAt">Set when the charge was voided. The row is listed and marked, never hidden.</param>
/// <param name="VoidedByFullName">Who voided it; <c>null</c> unless voided.</param>
/// <param name="NetPaid">Payments minus refunds against this charge.</param>
public sealed record HistoryServiceChargeResponse(
    Guid Id,
    Guid? MemberId,
    string? MemberFullName,
    string? GuestName,
    Guid AttendanceId,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ServiceChargeKind>))] ServiceChargeKind Kind,
    string? Description,
    int? Quantity,
    decimal? UnitPrice,
    decimal Amount,
    DateOnly ChargedOn,
    DateTimeOffset CreatedAt,
    string? RecordedByFullName,
    DateTimeOffset? VoidedAt,
    string? VoidReason,
    string? VoidedByFullName,
    decimal NetPaid,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentStatus>))] PaymentStatus PaymentStatus);
