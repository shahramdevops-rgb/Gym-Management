using Gym.Application.Common.Paging;

namespace Gym.Application.Staff.ListStaff;

public sealed class ListStaffHandler(IStaffAccounts staff)
{
    public Task<PagedResponse<StaffResponse>> Handle(ListStaffQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return staff.ListAsync(query.Page, query.PageSize, cancellationToken);
    }
}
