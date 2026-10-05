namespace Gym.Application.Payables.ListPayables;

/// <summary>
/// One page of the register, plus what is still pending (BUSINESS_RULES.md §9 <i>Cheques and
/// instalments</i>).
/// </summary>
/// <remarks>
/// The same fields as <c>PagedResponse</c> with three more, like <c>ExpenseListResponse</c>, so the
/// frontend's pager reads this list exactly as it reads every other one.
/// </remarks>
/// <param name="PendingTotal">
/// What the gym still has to pay: every pending cheque and instalment, past its date or not,
/// whatever the page is filtered by. The client cannot add it up itself: it sees one page.
/// </param>
/// <param name="PendingChequeTotal">The part of <paramref name="PendingTotal"/> that is cheques.</param>
/// <param name="PendingInstallmentTotal">The part of <paramref name="PendingTotal"/> that is instalments.</param>
public sealed record PayableListResponse(
    IReadOnlyList<PayableResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    decimal PendingTotal,
    decimal PendingChequeTotal,
    decimal PendingInstallmentTotal);
