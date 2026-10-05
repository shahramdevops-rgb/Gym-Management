namespace Gym.Application.Cheques.ListCheques;

/// <summary>
/// One page of cheques, plus the total of every pending cheque (BUSINESS_RULES.md §9 <i>Cheques</i>).
/// </summary>
/// <remarks>
/// The same fields as <c>PagedResponse</c> with one more, like <c>ExpenseListResponse</c>, so the
/// frontend's pager reads this list exactly as it reads every other one.
/// </remarks>
/// <param name="PendingTotal">
/// What the gym still has to pay on cheques it gave: every pending cheque, past its date or not,
/// whatever status the page is filtered by. The client cannot add it up itself: it sees one page.
/// </param>
public sealed record ChequeListResponse(
    IReadOnlyList<ChequeResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    decimal PendingTotal);
