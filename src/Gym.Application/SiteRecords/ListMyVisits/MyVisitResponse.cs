using System.Text.Json.Serialization;

using Gym.Application.Attendances;
using Gym.Application.ServiceCharges;
using Gym.Domain.Payments;
using Gym.Domain.ServiceCharges;

namespace Gym.Application.SiteRecords.ListMyVisits;

/// <summary>
/// One check-in and check-out with its locker (BUSINESS_RULES.md §15 <i>The member's own
/// records</i>): the panel's <see cref="AttendanceResponse"/> without the staff's fields. A
/// cancelled check-in is marked by <see cref="CancelledAt"/>.
/// </summary>
/// <param name="LockerNumber"><c>null</c> on a reserve place (<paramref name="UsesReservePlace"/>) or an old visit with no locker.</param>
/// <param name="AutoClosedAt">Set when the nightly job closed the visit (§7 Auto-checkout).</param>
/// <param name="Charges">The visit's non-voided charges (هوازی, a sale), as the panel lists them.</param>
public sealed record MyVisitResponse(
    Guid Id,
    DateTimeOffset CheckedInAt,
    DateTimeOffset? CheckedOutAt,
    int? LockerNumber,
    bool UsesReservePlace,
    bool IsCardioOnly,
    bool IsBirthdayVisit,
    DateTimeOffset? CancelledAt,
    DateTimeOffset? AutoClosedAt,
    IReadOnlyList<MyVisitChargeResponse> Charges)
{
    public static MyVisitResponse From(AttendanceResponse visit)
    {
        ArgumentNullException.ThrowIfNull(visit);

        return new MyVisitResponse(
            visit.Id,
            visit.CheckedInAt,
            visit.CheckedOutAt,
            visit.LockerNumber,
            visit.UsesReservePlace,
            visit.IsCardioOnly,
            visit.IsBirthdayVisit,
            visit.CancelledAt,
            visit.AutoClosedAt,
            visit.ServiceCharges.Select(MyVisitChargeResponse.From).ToList());
    }
}

/// <summary>A visit's charge without who recorded it.</summary>
/// <param name="Description">What a sale (فروشگاه, آنالیز) sold; <c>null</c> for هوازی.</param>
public sealed record MyVisitChargeResponse(
    Guid Id,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ServiceChargeKind>))] ServiceChargeKind Kind,
    string? Description,
    int? Quantity,
    decimal? UnitPrice,
    decimal Amount,
    decimal NetPaid,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentStatus>))] PaymentStatus PaymentStatus)
{
    public static MyVisitChargeResponse From(ServiceChargeResponse charge)
    {
        ArgumentNullException.ThrowIfNull(charge);

        return new MyVisitChargeResponse(
            charge.Id,
            charge.Kind,
            charge.Description,
            charge.Quantity,
            charge.UnitPrice,
            charge.Amount,
            charge.NetPaid,
            charge.PaymentStatus);
    }
}
