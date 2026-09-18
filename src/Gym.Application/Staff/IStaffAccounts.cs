using Gym.Application.Common.Paging;
using Gym.Domain.Common;

namespace Gym.Application.Staff;

/// <summary>
/// Staff accounts, as Identity stores them. Declared here and implemented in Infrastructure,
/// because Application cannot see the Identity <c>User</c> (ADR 0002).
/// </summary>
/// <remarks>
/// Every method treats an id that is not a Staff account, including the Owner's, as not found.
/// None of them touch refresh tokens or open transactions: the handlers do that, so the rules
/// about sessions stay in the use cases where they can be read.
/// </remarks>
public interface IStaffAccounts
{
    /// <summary>Creates the account in the Staff role with <c>MustChangePassword = true</c>.</summary>
    Task<Result<StaffResponse>> CreateAsync(
        string userName,
        string fullName,
        string temporaryPassword,
        CancellationToken cancellationToken);

    /// <summary>Staff only, ordered by full name then user name.</summary>
    Task<PagedResponse<StaffResponse>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<StaffResponse?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Idempotent: setting the state an account already has succeeds and changes nothing.</summary>
    Task<Result<StaffResponse>> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken);

    /// <summary>Sets a temporary password, requires a password change and clears any lockout.</summary>
    Task<Result> ResetPasswordAsync(Guid id, string temporaryPassword, CancellationToken cancellationToken);
}
