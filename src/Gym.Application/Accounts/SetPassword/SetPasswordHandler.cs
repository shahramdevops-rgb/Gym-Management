using Gym.Application.Auth;
using Gym.Application.Common;
using Gym.Domain.Auth;
using Gym.Domain.Common;
using Gym.Domain.Common.Text;

namespace Gym.Application.Accounts.SetPassword;

/// <summary>
/// <c>./server.sh set-password &lt;user&gt;</c>: a new password for any account, typed on the
/// server by the person it belongs to (BUSINESS_RULES.md §1 *Lockout*). The way back in when the
/// Owner has forgotten their password or is locked out, since nobody in the web app can reset
/// the Owner.
/// </summary>
/// <remarks>
/// Treated like a reset: every session ends and every trusted device is forgotten, because a
/// password set this way usually means the old one can no longer be trusted. Unlike a reset,
/// the user need not change it again, because they chose it themselves.
/// </remarks>
public sealed class SetPasswordHandler(IUserAccounts accounts, IAppDbContext db, TimeProvider timeProvider)
{
    public async Task<Result> Handle(string userName, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(password);

        var userId = await accounts.FindIdByUserNameAsync(userName, cancellationToken);
        if (userId is null)
        {
            return Result.Failure(AccountErrors.UserNotFound);
        }

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var result = await accounts.SetPasswordAsync(userId.Value, PersianText.NormalizeDigits(password), cancellationToken);
        if (result.IsFailure)
        {
            return result;
        }

        await db.RevokeAllForUserAsync(
            userId.Value, RefreshTokenRevocationReason.PasswordSetOnServer, timeProvider.GetUtcNow(), cancellationToken);
        await db.ForgetDevicesAsync(userId.Value, exceptTokenHash: null, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return result;
    }
}
