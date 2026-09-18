using Gym.Domain.Common;

namespace Gym.Domain.Auth;

/// <summary>
/// One refresh token, stored only as a hash. BUSINESS_RULES.md §1 has the rules.
/// </summary>
/// <remarks>
/// <para>
/// Every login starts a <b>family</b>: the first token and every token rotated from it share a
/// <see cref="FamilyId"/>. Each token can be used exactly once. Using it revokes it and creates
/// its child, and <see cref="ReplacedByTokenId"/> links the two. A token that is presented again
/// after it was rotated can only be a copy, so the application revokes the whole family and the
/// thief and the real user both have to log in again.
/// </para>
/// <para>
/// <see cref="UserId"/> is a plain id, not a reference to the Identity user, which lives in
/// Infrastructure (ADR 0002). The foreign key is still enforced by the database.
/// </para>
/// </remarks>
public sealed class RefreshToken : Entity
{
    /// <summary>BUSINESS_RULES.md §1: 7 days, renewed by every refresh.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    // For EF Core, which materializes rows through a parameterless constructor.
    private RefreshToken()
    {
    }

    private RefreshToken(Guid userId, string tokenHash, Guid familyId, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        UserId = userId;
        TokenHash = tokenHash;
        FamilyId = familyId;
        ExpiresAt = expiresAt;
    }

    public Guid UserId { get; private set; }

    /// <summary>
    /// The SHA-256 hash of the token. The token itself exists only in the user's cookie, so a
    /// copy of the database contains nothing that can be used to log in.
    /// </summary>
    public string TokenHash { get; private set; } = string.Empty;

    /// <summary>Shared by every token that descends from one login.</summary>
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

    /// <summary>Starts a new family. Called at login.</summary>
    public static RefreshToken Issue(Guid userId, string tokenHash, DateTimeOffset now) =>
        new(userId, tokenHash, Guid.CreateVersion7(), now + Lifetime);

    /// <summary>
    /// Uses this token up: revokes it and returns its replacement, in the same family, with a
    /// fresh <see cref="Lifetime"/>.
    /// </summary>
    public Result<RefreshToken> Rotate(string newTokenHash, DateTimeOffset now)
    {
        if (IsRevoked)
        {
            return Result.Failure<RefreshToken>(RefreshTokenErrors.Revoked);
        }

        if (IsExpired(now))
        {
            return Result.Failure<RefreshToken>(RefreshTokenErrors.Expired);
        }

        var child = new RefreshToken(UserId, newTokenHash, FamilyId, now + Lifetime);

        RevokedAt = now;
        RevokedReason = RefreshTokenRevocationReason.Rotated;
        ReplacedByTokenId = child.Id;

        return child;
    }

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
}
