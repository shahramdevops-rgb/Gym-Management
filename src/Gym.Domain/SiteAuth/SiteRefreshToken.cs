using Gym.Domain.Auth;
using Gym.Domain.Common;

namespace Gym.Domain.SiteAuth;

/// <summary>
/// One site refresh token (BUSINESS_RULES.md §15, ADR 0010): the same rules as the staff's
/// (<see cref="RotatingRefreshToken"/>), in a table of its own, for a <see cref="SiteAccount"/>.
/// </summary>
public sealed class SiteRefreshToken : RotatingRefreshToken
{
    /// <summary>
    /// BUSINESS_RULES.md §15: 60 days, renewed by every refresh, because every new sign-in on the
    /// site is a paid SMS.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(60);

    // For EF Core.
    private SiteRefreshToken()
    {
    }

    private SiteRefreshToken(Guid siteAccountId, string tokenHash, Guid familyId, DateTimeOffset expiresAt)
        : base(tokenHash, familyId, expiresAt)
    {
        SiteAccountId = siteAccountId;
    }

    public Guid SiteAccountId { get; private set; }

    /// <summary>Starts a new family. Called when the phone signs in with its code.</summary>
    public static SiteRefreshToken Issue(Guid siteAccountId, string tokenHash, DateTimeOffset now) =>
        new(siteAccountId, tokenHash, Guid.CreateVersion7(), now + Lifetime);

    /// <summary>Uses this token up and returns its child, with a fresh <see cref="Lifetime"/>.</summary>
    public Result<SiteRefreshToken> Rotate(string newTokenHash, DateTimeOffset now)
    {
        var allowed = CanRotate(now);
        if (allowed.IsFailure)
        {
            return Result.Failure<SiteRefreshToken>(allowed.Error);
        }

        var child = new SiteRefreshToken(SiteAccountId, newTokenHash, FamilyId, now + Lifetime);
        ReplaceWith(child, now);

        return child;
    }
}
