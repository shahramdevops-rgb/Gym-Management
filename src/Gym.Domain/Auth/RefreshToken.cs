using Gym.Domain.Common;

namespace Gym.Domain.Auth;

/// <summary>
/// One staff refresh token, stored only as a hash. BUSINESS_RULES.md §1 has the rules; what every
/// refresh token shares is in <see cref="RotatingRefreshToken"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every login starts a <b>family</b>: the first token and every token rotated from it share a
/// <see cref="RotatingRefreshToken.FamilyId"/>. Each token can be used exactly once. Using it
/// revokes it and creates its child, and <see cref="RotatingRefreshToken.ReplacedByTokenId"/> links
/// the two. A token that is presented again after it was rotated can only be a copy, so the
/// application revokes the whole family and the thief and the real user both have to log in again.
/// </para>
/// <para>
/// <see cref="UserId"/> is a plain id, not a reference to the Identity user, which lives in
/// Infrastructure (ADR 0002). The foreign key is still enforced by the database.
/// </para>
/// </remarks>
public sealed class RefreshToken : RotatingRefreshToken
{
    /// <summary>BUSINESS_RULES.md §1: 7 days, renewed by every refresh.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    // For EF Core, which materializes rows through a parameterless constructor.
    private RefreshToken()
    {
    }

    private RefreshToken(Guid userId, string tokenHash, Guid familyId, DateTimeOffset expiresAt)
        : base(tokenHash, familyId, expiresAt)
    {
        UserId = userId;
    }

    public Guid UserId { get; private set; }

    /// <summary>Starts a new family. Called at login.</summary>
    public static RefreshToken Issue(Guid userId, string tokenHash, DateTimeOffset now) =>
        new(userId, tokenHash, Guid.CreateVersion7(), now + Lifetime);

    /// <summary>
    /// Uses this token up: revokes it and returns its replacement, in the same family, with a
    /// fresh <see cref="Lifetime"/>.
    /// </summary>
    public Result<RefreshToken> Rotate(string newTokenHash, DateTimeOffset now)
    {
        var allowed = CanRotate(now);
        if (allowed.IsFailure)
        {
            return Result.Failure<RefreshToken>(allowed.Error);
        }

        var child = new RefreshToken(UserId, newTokenHash, FamilyId, now + Lifetime);
        ReplaceWith(child, now);

        return child;
    }
}
