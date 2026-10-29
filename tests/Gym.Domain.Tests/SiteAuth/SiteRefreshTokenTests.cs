using Gym.Domain.Auth;
using Gym.Domain.SiteAuth;

namespace Gym.Domain.Tests.SiteAuth;

/// <summary>
/// The site's refresh token: the staff's rules (<c>RefreshTokenTests</c>, through the shared
/// <see cref="RotatingRefreshToken"/>) with the site's 60 days (BUSINESS_RULES.md §15).
/// </summary>
public sealed class SiteRefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid AccountId = Guid.CreateVersion7();

    [Fact]
    public void Issue_WhenCalled_LivesSixtyDays()
    {
        var token = SiteRefreshToken.Issue(AccountId, "hash-1", Now);

        token.SiteAccountId.ShouldBe(AccountId);
        token.ExpiresAt.ShouldBe(Now.AddDays(60));
        token.IsActive(Now.AddDays(59)).ShouldBeTrue();
        token.IsActive(Now.AddDays(60)).ShouldBeFalse();
    }

    [Fact]
    public void Rotate_ActiveToken_GivesAChildInTheSameFamilyWithAFreshSixtyDays()
    {
        var parent = SiteRefreshToken.Issue(AccountId, "hash-1", Now);
        var later = Now.AddDays(30);

        var child = parent.Rotate("hash-2", later).Value;

        child.FamilyId.ShouldBe(parent.FamilyId);
        child.SiteAccountId.ShouldBe(AccountId);
        child.ExpiresAt.ShouldBe(later.AddDays(60));
        parent.WasRotated.ShouldBeTrue();
        parent.ReplacedByTokenId.ShouldBe(child.Id);
    }

    [Fact]
    public void Rotate_RevokedToken_IsRefused()
    {
        var token = SiteRefreshToken.Issue(AccountId, "hash-1", Now);
        token.Revoke(RefreshTokenRevocationReason.Logout, Now);

        token.Rotate("hash-2", Now).Error.ShouldBe(RefreshTokenErrors.Revoked);
    }

    [Fact]
    public void Rotate_ExpiredToken_IsRefused()
    {
        var token = SiteRefreshToken.Issue(AccountId, "hash-1", Now);

        token.Rotate("hash-2", Now.AddDays(60)).Error.ShouldBe(RefreshTokenErrors.Expired);
    }

    [Fact]
    public void Revoke_AlreadyRotated_KeepsTheRecordOfRotation()
    {
        var token = SiteRefreshToken.Issue(AccountId, "hash-1", Now);
        token.Rotate("hash-2", Now);

        token.Revoke(RefreshTokenRevocationReason.ReuseDetected, Now.AddMinutes(1));

        token.RevokedReason.ShouldBe(RefreshTokenRevocationReason.Rotated);
    }
}
