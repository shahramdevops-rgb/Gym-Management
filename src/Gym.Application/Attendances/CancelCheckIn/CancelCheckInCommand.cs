namespace Gym.Application.Attendances.CancelCheckIn;

/// <summary>
/// What the desk chose to do with the visit's purchases (BUSINESS_RULES.md §7 <i>Cancel
/// check-in</i>). All three are required: the server does not guess, and a missing choice is
/// refused rather than read as "keep" or "cancel".
/// </summary>
/// <param name="VoidCardio">
/// <c>true</c> voids the visit's هوازی, refunding what was paid on it; <c>false</c> leaves it owed.
/// <c>true</c> on a visit with no هوازی simply has nothing to void.
/// </param>
/// <param name="CafeOrderIds">
/// The visit's cafe orders to cancel, each ticked on its own; empty keeps them all. Orders not
/// named stay on the member's account, including one added after the box was opened.
/// </param>
/// <param name="SaleIds">
/// The visit's sales to void, فروشگاه and آنالیز alike, each ticked on its own, exactly like the
/// cafe orders; empty keeps them all.
/// </param>
public sealed record CancelCheckInCommand(
    bool? VoidCardio,
    IReadOnlyList<Guid>? CafeOrderIds,
    IReadOnlyList<Guid>? SaleIds);
