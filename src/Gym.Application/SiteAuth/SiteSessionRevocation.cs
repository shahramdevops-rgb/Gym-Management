using Gym.Application.Common;
using Gym.Domain.Auth;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.SiteAuth;

/// <summary>
/// Ends every live session of some site accounts: the desk changing a member's phone, the Owner
/// changing a trainer's (BUSINESS_RULES.md §15). The site's twin of <c>RefreshTokenRevocation</c>.
/// </summary>
public static class SiteSessionRevocation
{
    /// <summary>
    /// Revokes each unrevoked refresh token of the accounts through the entity's own method. Does
    /// not save: the caller saves with its own change.
    /// </summary>
    public static async Task RevokeSiteSessionsAsync(
        this IAppDbContext db,
        IQueryable<Guid> siteAccountIds,
        RefreshTokenRevocationReason reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        var sessions = await db.SiteRefreshTokens
            .Where(token => token.RevokedAt == null && siteAccountIds.Contains(token.SiteAccountId))
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.Revoke(reason, now);
        }
    }
}
