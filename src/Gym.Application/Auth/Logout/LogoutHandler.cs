using Gym.Application.Common;
using Gym.Application.Common.Security;
using Gym.Domain.Auth;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Auth.Logout;

/// <summary>
/// Revokes the presented refresh token. Always succeeds (BUSINESS_RULES.md §1).
/// </summary>
/// <remarks>
/// A logout that reported "unknown token" would let anyone test whether a guessed token
/// exists. And a user who clicks "log out" is logged out as far as they are concerned, whatever
/// state their cookie was in; the endpoint clears the cookie either way.
/// </remarks>
public sealed class LogoutHandler(IAppDbContext db, TimeProvider timeProvider)
{
    public async Task<Result> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrEmpty(command.RefreshToken))
        {
            return Result.Success();
        }

        var hash = RefreshTokenSecret.Hash(command.RefreshToken);
        var token = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        // Only the active token matters: every older token in the family was already revoked
        // by rotation, so revoking this one ends the whole login session.
        var now = timeProvider.GetUtcNow();
        if (token is not null && token.IsActive(now))
        {
            token.Revoke(RefreshTokenRevocationReason.Logout, now);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
