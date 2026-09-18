using Gym.Application.Common;
using Gym.Domain.Auth;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Auth;

/// <summary>
/// Ends every session a user has. Used by change password, deactivate and reset password.
/// </summary>
public static class RefreshTokenRevocation
{
    /// <summary>
    /// Revokes each of the user's unrevoked refresh tokens through the entity's own method. Does
    /// not save: the caller saves as part of its own unit of work, usually inside a transaction.
    /// </summary>
    public static async Task RevokeAllForUserAsync(
        this IAppDbContext db,
        Guid userId,
        RefreshTokenRevocationReason reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        var activeTokens = await db.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Revoke(reason, now);
        }
    }
}
