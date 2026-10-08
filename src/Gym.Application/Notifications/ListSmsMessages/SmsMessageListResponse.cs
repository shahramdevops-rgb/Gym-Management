namespace Gym.Application.Notifications.ListSmsMessages;

/// <summary>
/// One page of the SMS history, plus what every message the filter matches cost, across all pages
/// (BUSINESS_RULES.md §10 <i>The SMS history</i>).
/// </summary>
/// <remarks>
/// The same fields as <c>PagedResponse</c> with one more, like the expenses list, so the frontend's
/// pager reads it as it reads every other list.
/// </remarks>
/// <param name="TotalCostToman">
/// The filter's cost in Toman. Opened on the current Jalali month, it is the month's total. The
/// client cannot add it up itself: it sees one page.
/// </param>
public sealed record SmsMessageListResponse(
    IReadOnlyList<SmsMessageResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    decimal TotalCostToman);
