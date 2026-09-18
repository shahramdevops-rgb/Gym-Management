using Gym.Application.Auth;
using Gym.Application.Common;
using Gym.Domain.Auth;
using Gym.Domain.Common;

namespace Gym.Application.Staff.SetStaffActive;

/// <summary>
/// Deactivates or reactivates a staff account (BUSINESS_RULES.md §1).
/// </summary>
/// <remarks>
/// Deactivation revokes every refresh token in the same transaction, so the moment the Owner
/// sees "deactivated" the account can no longer get a new access token. The access token it
/// already holds lives out its 15 minutes; that window is the price of tokens the server does
/// not look up on every request.
/// </remarks>
public sealed class SetStaffActiveHandler(IStaffAccounts staff, IAppDbContext db, TimeProvider timeProvider)
{
    public async Task<Result<StaffResponse>> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var result = await staff.SetActiveAsync(id, isActive: false, cancellationToken);
        if (result.IsFailure)
        {
            return result;
        }

        await db.RevokeAllForUserAsync(id, RefreshTokenRevocationReason.UserInactive, timeProvider.GetUtcNow(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return result;
    }

    /// <summary>The password is untouched: the user logs in with the one they had.</summary>
    public Task<Result<StaffResponse>> Reactivate(Guid id, CancellationToken cancellationToken) =>
        staff.SetActiveAsync(id, isActive: true, cancellationToken);
}
