using Gym.Application.Common;
using Gym.Application.Common.Security;
using Gym.Domain.Auth;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Auth.ChangePassword;

/// <summary>
/// Changes the logged-in user's password and starts a fresh session (BUSINESS_RULES.md §1).
/// </summary>
/// <remarks>
/// <para>
/// Every refresh token the user holds is revoked, which logs out every other browser. If the
/// password is being changed because it leaked, those sessions are exactly the ones to end.
/// </para>
/// <para>
/// The browser that made the change gets a new session straight away. Its old access token
/// still says <c>must_change_password=true</c>, and the new one does not, so the user goes
/// on to the app without logging in again.
/// </para>
/// </remarks>
public sealed class ChangePasswordHandler(
    IAppDbContext db,
    IUserAuthenticator users,
    IAccessTokenIssuer tokenIssuer,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<AuthSession>> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // The endpoint's policy requires an authenticated user, so this is a wiring bug if hit.
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Change password was called without an authenticated user.");

        // Outside the transaction on purpose: a wrong password must still count toward lockout,
        // and rolling back would erase that count.
        var verified = await users.VerifyPasswordAsync(userId, command.CurrentPassword, cancellationToken);
        if (verified.IsFailure)
        {
            return Result.Failure<AuthSession>(verified.Error);
        }

        // Checked after the current password, so it reveals nothing to someone who does not
        // know it. Ordinal: passwords are compared exactly, as the hash would compare them.
        if (string.Equals(command.CurrentPassword, command.NewPassword, StringComparison.Ordinal))
        {
            return Result.Failure<AuthSession>(AuthErrors.PasswordUnchanged);
        }

        var now = timeProvider.GetUtcNow();

        // The new password, the cleared flag and the revoked sessions happen together or not
        // at all. Committed password with live old sessions would defeat the point of changing it.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var changed = await users.ChangePasswordAsync(userId, command.CurrentPassword, command.NewPassword, cancellationToken);
        if (changed.IsFailure)
        {
            return Result.Failure<AuthSession>(changed.Error);
        }

        var activeTokens = await db.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Revoke(RefreshTokenRevocationReason.PasswordChanged, now);
        }

        var refreshSecret = RefreshTokenSecret.Generate();
        var refreshToken = RefreshToken.Issue(userId, RefreshTokenSecret.Hash(refreshSecret), now);
        db.RefreshTokens.Add(refreshToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var user = changed.Value;

        return AuthSession.Create(user, tokenIssuer.Issue(user), refreshSecret, refreshToken.ExpiresAt);
    }
}
