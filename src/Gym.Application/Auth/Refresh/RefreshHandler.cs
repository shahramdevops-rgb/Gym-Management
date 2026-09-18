using Gym.Application.Common;
using Gym.Application.Common.Security;
using Gym.Domain.Auth;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Application.Auth.Refresh;

/// <summary>
/// Trades a refresh token for a new access token and a new refresh token (rotation).
/// </summary>
/// <remarks>
/// <para>
/// A token that was already rotated can only come back if someone copied it, so presenting one
/// revokes the whole family (BUSINESS_RULES.md §1). The copy and the original then both stop
/// working, and the real user logs in again with their password, which the thief does not have.
/// </para>
/// <para>
/// Two requests rotating the same token at the same moment both read it as active. The
/// <c>xmin</c> concurrency token makes the second save fail, and the rules treat that the same
/// as reuse: strict, with no grace period.
/// </para>
/// </remarks>
public sealed partial class RefreshHandler(
    IAppDbContext db,
    IUserAuthenticator users,
    IAccessTokenIssuer tokenIssuer,
    TimeProvider timeProvider,
    ILogger<RefreshHandler> logger)
{
    public async Task<Result<AuthSession>> Handle(RefreshCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrEmpty(command.RefreshToken))
        {
            return Invalid();
        }

        var hash = RefreshTokenSecret.Hash(command.RefreshToken);
        var token = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is null)
        {
            return Invalid();
        }

        var now = timeProvider.GetUtcNow();

        if (token.WasRotated)
        {
            LogReuseDetected(logger, token.FamilyId, token.UserId);
            await RevokeFamilyAsync(token.FamilyId, RefreshTokenRevocationReason.ReuseDetected, now, cancellationToken);

            return Invalid();
        }

        // Revoked for another reason (logout, deactivation) or simply too old.
        if (!token.IsActive(now))
        {
            return Invalid();
        }

        var user = await users.GetActiveUserAsync(token.UserId, cancellationToken);
        if (user.IsFailure)
        {
            await RevokeFamilyAsync(token.FamilyId, RefreshTokenRevocationReason.UserInactive, now, cancellationToken);

            return Result.Failure<AuthSession>(user.Error);
        }

        var refreshSecret = RefreshTokenSecret.Generate();

        // Cannot fail: the token was checked as active a moment ago, with the same "now".
        var child = token.Rotate(RefreshTokenSecret.Hash(refreshSecret), now).Value;
        db.RefreshTokens.Add(child);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request rotated this token between our read and our save.
            LogConcurrentRotation(logger, token.FamilyId, token.UserId);

            // The tracked token and child hold values that never reached the database.
            db.ChangeTracker.Clear();
            await RevokeFamilyAsync(token.FamilyId, RefreshTokenRevocationReason.ReuseDetected, now, cancellationToken);

            return Invalid();
        }

        return AuthSession.Create(user.Value, tokenIssuer.Issue(user.Value), refreshSecret, child.ExpiresAt);
    }

    /// <summary>
    /// Revokes every token in the family that is still unrevoked, through the entity's own
    /// method, so each keeps an accurate record of why it ended.
    /// </summary>
    /// <remarks>
    /// Revocation races too: a rotation or another reuse detection can change a family member
    /// between the read and the save. Each conflict means another request moved the family on,
    /// so reading it again and retrying makes progress; giving up would leave a family alive
    /// that was meant to die, and surface as a 500. The bound is a guard against a bug, not a
    /// limit real traffic reaches.
    /// </remarks>
    private async Task RevokeFamilyAsync(
        Guid familyId,
        RefreshTokenRevocationReason reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 10;

        for (var attempt = 1; ; attempt++)
        {
            var family = await db.RefreshTokens
                .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var member in family)
            {
                member.Revoke(reason, now);
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

    private static Result<AuthSession> Invalid() => Result.Failure<AuthSession>(AuthErrors.RefreshTokenInvalid);

    // Warnings, not errors: reuse is either an attack being stopped or two browser tabs racing.
    // Either way the system did the right thing, but the Owner may want to look at it.
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Refresh token reuse detected; revoked token family {FamilyId} of user {UserId}.")]
    private static partial void LogReuseDetected(ILogger logger, Guid familyId, Guid userId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Concurrent rotation of one refresh token; revoked token family {FamilyId} of user {UserId}.")]
    private static partial void LogConcurrentRotation(ILogger logger, Guid familyId, Guid userId);
}
