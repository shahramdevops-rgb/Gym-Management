using Gym.Domain.Common;

namespace Gym.Domain.Auth;

/// <summary>
/// What every refresh token has in common, the staff's (<see cref="RefreshToken"/>) and the
/// site's (<c>SiteRefreshToken</c>): stored only as a hash, used exactly once, and rotated into a
/// child of the same family. BUSINESS_RULES.md §1 has the rules; §15 gives the site its own lifetime.
/// </summary>
/// <remarks>
/// <para>
/// One class for the rules, two tables for the rows (ADR 0010: staff and site sign-ins never
/// mix). Each subclass adds whose token it is and how long it lives, and builds its own child; the
/// checks that decide whether a token may be rotated, and the record of how it ended, are here
/// once, so the two kinds of sign-in can never drift apart on them.
/// </para>
/// <para>
/// Not an entity of its own: EF Core maps these properties as part of each subclass's table.
/// </para>
/// </remarks>
public abstract class RotatingRefreshToken : Entity
{
    // For EF Core, through the subclasses.
    protected RotatingRefreshToken()
    {
    }

    protected RotatingRefreshToken(string tokenHash, Guid familyId, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        TokenHash = tokenHash;
        FamilyId = familyId;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// The SHA-256 hash of the token. The token itself exists only in the browser's cookie, so a
    /// copy of the database contains nothing that can be used to sign in.
    /// </summary>
    public string TokenHash { get; private set; } = string.Empty;

    /// <summary>Shared by every token that descends from one sign-in.</summary>
    public Guid FamilyId { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public RefreshTokenRevocationReason? RevokedReason { get; private set; }

    /// <summary>The child created when this token was rotated. Null for every other revocation.</summary>
    public Guid? ReplacedByTokenId { get; private set; }

    /// <summary>
    /// Postgres <c>xmin</c>. Two requests rotating the same token at once both read it as
    /// active; this makes the second save fail instead of creating two children.
    /// </summary>
    public uint Version { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    /// <summary>Whether this token was already used once. Presenting it again is reuse.</summary>
    public bool WasRotated => RevokedReason == RefreshTokenRevocationReason.Rotated;

    /// <summary>Expired from the exact moment of <see cref="ExpiresAt"/>, not a moment later.</summary>
    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public bool IsActive(DateTimeOffset now) => !IsRevoked && !IsExpired(now);

    /// <summary>
    /// Does nothing if the token is already revoked, so revoking a whole family never
    /// overwrites the record of why each token ended.
    /// </summary>
    public void Revoke(RefreshTokenRevocationReason reason, DateTimeOffset now)
    {
        if (reason == RefreshTokenRevocationReason.Rotated)
        {
            throw new ArgumentException("Use Rotate to rotate a token.", nameof(reason));
        }

        if (IsRevoked)
        {
            return;
        }

        RevokedAt = now;
        RevokedReason = reason;
    }

    /// <summary>Whether this token may be rotated now: not revoked, not expired.</summary>
    protected Result CanRotate(DateTimeOffset now)
    {
        if (IsRevoked)
        {
            return Result.Failure(RefreshTokenErrors.Revoked);
        }

        return IsExpired(now) ? Result.Failure(RefreshTokenErrors.Expired) : Result.Success();
    }

    /// <summary>Uses this token up in favour of <paramref name="child"/>, after <see cref="CanRotate"/>.</summary>
    protected void ReplaceWith(RotatingRefreshToken child, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(child);

        RevokedAt = now;
        RevokedReason = RefreshTokenRevocationReason.Rotated;
        ReplacedByTokenId = child.Id;
    }
}
