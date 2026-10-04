using System.Text.Json.Serialization;

using Gym.Domain.Payments;
using Gym.Domain.ServiceCharges;

namespace Gym.Application.GuestDebts.ListGuestDebts;

/// <summary>
/// One purchase of a guest's visit that still owes money (BUSINESS_RULES.md §7 <i>Guest visit</i>,
/// «بدهی مهمان‌ها»): a cafe order or a service charge, with what is left to pay on it.
/// </summary>
/// <param name="Target">
/// Which table the row is from, so the screen opens that item's own payment and void or cancel
/// forms: <see cref="PaymentTargetKind.CafeOrder"/> or <see cref="PaymentTargetKind.ServiceCharge"/>.
/// </param>
/// <param name="Id">The cafe order or service charge.</param>
/// <param name="AttendanceId">The guest's visit.</param>
/// <param name="GuestName">The name typed at the desk for the visit.</param>
/// <param name="Day">The business date: the order's <c>OrderedOn</c> or the charge's <c>ChargedOn</c>.</param>
/// <param name="RecordedAt">The moment it was recorded, for the time on screen and the order of the list.</param>
/// <param name="ServiceKind">هوازی, فروشگاه or آنالیز; <c>null</c> for a cafe order.</param>
/// <param name="Description">What a فروشگاه item was; <c>null</c> otherwise.</param>
/// <param name="Quantity">How many of a فروشگاه item; <c>null</c> otherwise.</param>
/// <param name="CafeItems">What a cafe order held; <c>null</c> unless a cafe order.</param>
/// <param name="NetPaid">Payments minus refunds against it.</param>
/// <param name="Outstanding">What is still owed: the amount less what has been paid.</param>
/// <param name="VisitIsOpen">The guest is still inside, so the debt can also be settled from their locker.</param>
public sealed record GuestDebtResponse(
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentTargetKind>))] PaymentTargetKind Target,
    Guid Id,
    Guid AttendanceId,
    string GuestName,
    DateOnly Day,
    DateTimeOffset RecordedAt,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ServiceChargeKind>))] ServiceChargeKind? ServiceKind,
    string? Description,
    int? Quantity,
    IReadOnlyList<GuestDebtCafeItem>? CafeItems,
    decimal Amount,
    decimal NetPaid,
    decimal Outstanding,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentStatus>))] PaymentStatus PaymentStatus,
    bool VisitIsOpen);

/// <summary>One line of a guest's cafe order: the product's name as sold, and how many.</summary>
public sealed record GuestDebtCafeItem(string ProductName, int Quantity);
