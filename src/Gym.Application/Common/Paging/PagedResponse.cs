namespace Gym.Application.Common.Paging;

/// <summary>
/// One page of a list, plus what the frontend needs to draw the pager.
/// </summary>
/// <remarks>
/// Every list endpoint is paged (docs/ARCHITECTURE.md), even ones that are short today: a list
/// that is unbounded by design becomes a slow page the day the table grows, and changing a
/// response shape later breaks the frontend.
/// </remarks>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
