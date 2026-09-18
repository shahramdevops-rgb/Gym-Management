using Gym.Domain.Common;

namespace Gym.Application.Staff.GetStaff;

public sealed class GetStaffHandler(IStaffAccounts staff)
{
    public async Task<Result<StaffResponse>> Handle(Guid id, CancellationToken cancellationToken)
    {
        var account = await staff.FindAsync(id, cancellationToken);

        return account is null ? Result.Failure<StaffResponse>(StaffErrors.NotFound) : account;
    }
}
