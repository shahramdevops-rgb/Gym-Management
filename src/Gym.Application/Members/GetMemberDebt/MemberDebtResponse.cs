namespace Gym.Application.Members.GetMemberDebt;

/// <param name="Total">
/// The sum of <paramref name="Items"/>. The front desk never sees it on its own: opening it shows
/// the breakdown, item by item (BUSINESS_RULES.md §5 <i>Member debt</i>).
/// </param>
/// <param name="Items">
/// Only what is still owed, newest first. A cancelled, free or fully paid subscription owes
/// nothing and is not here.
/// </param>
public sealed record MemberDebtResponse(decimal Total, IReadOnlyList<MemberDebtItemResponse> Items);

/// <param name="PlanName">
/// The plan's name <i>now</i>, like every other place a sold subscription is shown
/// (BUSINESS_RULES.md §4).
/// </param>
/// <param name="Outstanding"><c>Price − NetPaid</c>: what the member still owes on this item.</param>
public sealed record MemberDebtItemResponse(
    Guid SubscriptionId,
    string PlanName,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal Price,
    decimal NetPaid,
    decimal Outstanding);
