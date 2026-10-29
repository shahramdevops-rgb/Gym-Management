using Gym.Application.Common.Paging;

namespace Gym.Application.SiteRecords;

/// <summary>A page of the panel's list turned into the site's rows, with the same pager.</summary>
internal static class SitePages
{
    public static PagedResponse<TSite> Map<TPanel, TSite>(PagedResponse<TPanel> page, Func<TPanel, TSite> map) =>
        new(page.Items.Select(map).ToList(), page.Page, page.PageSize, page.TotalCount);
}
