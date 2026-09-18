using Gym.Application.Auth;
using Gym.Application.Common;
using Gym.Application.Common.Text;
using Gym.Domain.Auth;
using Gym.Domain.Common;

namespace Gym.Application.Staff.ResetStaffPassword;

/// <summary>
/// The Owner gives a staff member a new temporary password, for example when they forgot theirs
/// or are locked out (BUSINESS_RULES.md §1).
/// </summary>
/// <remarks>
/// Every session ends, because a reset usually means the old password can no longer be
/// trusted, and whoever held a session with it should not keep one.
/// </remarks>
public sealed class ResetStaffPasswordHandler(IStaffAccounts staff, IAppDbContext db, TimeProvider timeProvider)
{
    public async Task<Result> Handle(Guid id, ResetStaffPasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var result = await staff.ResetPasswordAsync(id, Digits.ToEnglish(command.TemporaryPassword), cancellationToken);
        if (result.IsFailure)
        {
            return result;
        }

        await db.RevokeAllForUserAsync(id, RefreshTokenRevocationReason.PasswordReset, timeProvider.GetUtcNow(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return result;
    }
}
