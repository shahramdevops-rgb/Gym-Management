using Gym.Application.Common;
using Gym.Application.Common.Security;
using Gym.Domain.Auth;
using Gym.Domain.Common;
using Gym.Domain.SiteAuth;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Application.SiteAuth.Refresh;

/// <summary>
/// Trades the site's refresh cookie for a new access token and a new refresh token, as the staff's
/// <c>RefreshHandler</c> does (BUSINESS_RULES.md §1 and §15), with the site's 60 days.
/// </summary>
/// <remarks>
/// A rotated token that comes back can only be a copy, so its whole family is revoked, and two
/// rotations of one token at the same moment are treated the same way (<c>xmin</c>). A member the
/// desk deactivated still refreshes: they may sign in, and the site shows them only the notice.
/// </remarks>
public sealed partial class SiteRefreshHandler(
    IAppDbContext db,
    SiteSessions sessions,
    TimeProvider timeProvider,
    ILogger<SiteRefreshHandler> logger)
{
    public async Task<Result<SiteSession>> Handle(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Invalid();
        }

        var hash = RefreshTokenSecret.Hash(refreshToken);
        var token = await db.SiteRefreshTokens.SingleOrDefaultAsync(row => row.TokenHash == hash, cancellationToken);
        if (token is null)
        {
            return Invalid();
        }

        var now = timeProvider.GetUtcNow();

        if (token.WasRotated)
        {
            LogReuseDetected(logger, token.FamilyId, token.SiteAccountId);
            await RevokeFamilyAsync(token.FamilyId, now, cancellationToken);

            return Invalid();
        }

        if (!token.IsActive(now))
        {
            return Invalid();
        }

        var account = await db.SiteAccounts.SingleAsync(row => row.Id == token.SiteAccountId, cancellationToken);

        // The roles are read again, so a trainer the Owner stopped loses the trainer's area here.
        var person = await SitePerson.LoadAsync(db, account, cancellationToken);

        var refreshSecret = RefreshTokenSecret.Generate();

        // Cannot fail: the token was checked as active a moment ago, with the same "now".
        var child = token.Rotate(RefreshTokenSecret.Hash(refreshSecret), now).Value;
        db.SiteRefreshTokens.Add(child);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            LogConcurrentRotation(logger, token.FamilyId, token.SiteAccountId);

            db.ChangeTracker.Clear();
            await RevokeFamilyAsync(token.FamilyId, now, cancellationToken);

            return Invalid();
        }

        return sessions.For(account, person, refreshSecret, child.ExpiresAt);
    }

    /// <summary>Every unrevoked token of the family, retried as the staff's is when a save races.</summary>
    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        const int maxAttempts = 10;

        for (var attempt = 1; ; attempt++)
        {
            var family = await db.SiteRefreshTokens
                .Where(row => row.FamilyId == familyId && row.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var member in family)
            {
                member.Revoke(RefreshTokenRevocationReason.ReuseDetected, now);
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxAttempts)
            {
                db.ChangeTracker.Clear();
            }
        }
    }

    private static Result<SiteSession> Invalid() => Result.Failure<SiteSession>(SiteAuthErrors.SessionInvalid);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Site refresh token reuse detected; revoked token family {FamilyId} of site account {SiteAccountId}.")]
    private static partial void LogReuseDetected(ILogger logger, Guid familyId, Guid siteAccountId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Concurrent rotation of one site refresh token; revoked token family {FamilyId} of site account {SiteAccountId}.")]
    private static partial void LogConcurrentRotation(ILogger logger, Guid familyId, Guid siteAccountId);
}
