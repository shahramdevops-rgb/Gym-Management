using System.Text.Json.Serialization;

namespace Gym.Application.Attendances.CheckIn;

/// <param name="LockerId">
/// The place the desk chose for the visit (BUSINESS_RULES.md §7, step 3): the locker it clicked,
/// or <c>null</c> for a reserve place, which only works when every locker is full (§6).
/// </param>
/// <param name="Sale">
/// Something to sell the member first, when they have nothing usable today: a single visit or a
/// plan. The sale and the check-in are one transaction, so a subscription sold at the locker always
/// comes with that locker (§7 <i>Confirming at the front desk</i>, roadmap 6.5.7). <c>null</c> for
/// an ordinary check-in.
/// </param>
public sealed record CheckInCommand(Guid? LockerId, CheckInSale? Sale = null);

/// <param name="Kind">What is sold.</param>
/// <param name="DurationDays">A plan's days (§3). Only for <see cref="CheckInSaleKind.Membership"/>.</param>
/// <param name="SessionCount">A plan's sessions (§3). Only for <see cref="CheckInSaleKind.Membership"/>.</param>
public sealed record CheckInSale(
    [property: JsonConverter(typeof(JsonStringEnumConverter<CheckInSaleKind>))] CheckInSaleKind Kind,
    int? DurationDays = null,
    int? SessionCount = null);

public enum CheckInSaleKind
{
    /// <summary>One visit for today at the single-visit price (BUSINESS_RULES.md §4).</summary>
    SingleVisit,

    /// <summary>A plan of so many days and sessions at the session price (BUSINESS_RULES.md §3).</summary>
    Membership,
}
