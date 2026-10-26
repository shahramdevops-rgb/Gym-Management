using Gym.Application.Common;
using Gym.Application.Common.Security;
using Gym.Domain.Auth;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.SiteAuth.Logout;

/// <summary>
/// Revokes the site's refresh token. Always succeeds, like the staff's logout: an answer of
/// "unknown token" would only help someone testing guessed tokens.
/// </summary>
public sealed class SiteLogoutHandler(IAppDbContext db, TimeProvider timeProvider)
{
    public async Task<Result> Handle(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Result.Success();
        }

        var hash = RefreshTokenSecret.Hash(refreshToken);
        var token = await db.SiteRefreshTokens.SingleOrDefaultAsync(row => row.TokenHash == hash, cancellationToken);

        // Only the active token matters: the older ones of its family were revoked by rotation.
        var now = timeProvider.GetUtcNow();
        if (token is not null && token.IsActive(now))
        {
            token.Revoke(RefreshTokenRevocationReason.Logout, now);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
