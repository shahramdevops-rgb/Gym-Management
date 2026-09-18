using Gym.Application.Common;
using Gym.Domain.Common.Text;
using Gym.Domain.Common;

namespace Gym.Application.Staff.CreateStaff;

/// <summary>The Owner creates a staff account with a temporary password (BUSINESS_RULES.md §1).</summary>
public sealed class CreateStaffHandler(IStaffAccounts staff, IAppDbContext db)
{
    public async Task<Result<StaffResponse>> Handle(CreateStaffCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Creating the user and adding the role are two saves inside Identity. Without one
        // transaction, a failure between them would leave an account with no role: it could
        // log in, pass no policy, and block its user name from being used again.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var created = await staff.CreateAsync(
            command.UserName.Trim(),
            StaffNames.Clean(command.FullName),
            PersianText.NormalizeDigits(command.TemporaryPassword),
            cancellationToken);

        if (created.IsSuccess)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return created;
    }
}
