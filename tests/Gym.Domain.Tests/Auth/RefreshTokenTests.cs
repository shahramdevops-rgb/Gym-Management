using Gym.Domain.Auth;

namespace Gym.Domain.Tests.Auth;

public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 18, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.CreateVersion7();

    [Fact]
    public void Issue_WhenCalled_StartsAnActiveTokenThatExpiresInSevenDays()
    {
        var token = RefreshToken.Issue(UserId, "hash-1", Now);

        token.UserId.ShouldBe(UserId);
        token.TokenHash.ShouldBe("hash-1");
        token.ExpiresAt.ShouldBe(Now.AddDays(7));
        token.IsActive(Now).ShouldBeTrue();
    }

    [Fact]
    public void Issue_CalledTwice_StartsTwoFamilies()
    {
        var first = RefreshToken.Issue(UserId, "hash-1", Now);
        var second = RefreshToken.Issue(UserId, "hash-2", Now);

        first.FamilyId.ShouldNotBe(second.FamilyId);
    }

    [Fact]
    public void Rotate_ActiveToken_RevokesItAndReturnsAChildInTheSameFamily()
    {
        var parent = RefreshToken.Issue(UserId, "hash-1", Now);
        var later = Now.AddDays(3);

        var child = parent.Rotate("hash-2", later).Value;

        child.FamilyId.ShouldBe(parent.FamilyId);
        child.UserId.ShouldBe(UserId);
        child.TokenHash.ShouldBe("hash-2");
        child.IsActive(later).ShouldBeTrue();

        parent.IsActive(later).ShouldBeFalse();
        parent.WasRotated.ShouldBeTrue();
        parent.RevokedAt.ShouldBe(later);
        parent.ReplacedByTokenId.ShouldBe(child.Id);
    }

    [Fact]
    public void Rotate_ActiveToken_GivesTheChildAFullSevenDays()
    {
        var parent = RefreshToken.Issue(UserId, "hash-1", Now);
        var later = Now.AddDays(6);

        var child = parent.Rotate("hash-2", later).Value;

        // Sliding: using the app renews the 7 days instead of counting down from login.
        child.ExpiresAt.ShouldBe(later.AddDays(7));
    }

    [Fact]
    public void Rotate_AlreadyRotatedToken_FailsWithRevoked()
    {
        var parent = RefreshToken.Issue(UserId, "hash-1", Now);
        parent.Rotate("hash-2", Now);

        var result = parent.Rotate("hash-3", Now);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RefreshTokenErrors.Revoked);
    }

    [Fact]
    public void Rotate_ExpiredToken_FailsWithExpired()
    {
        var token = RefreshToken.Issue(UserId, "hash-1", Now);

        var result = token.Rotate("hash-2", token.ExpiresAt);

        result.Error.ShouldBe(RefreshTokenErrors.Expired);
        token.IsRevoked.ShouldBeFalse();
    }

    [Fact]
    public void IsActive_AtTheExactExpiryMoment_IsFalse()
    {
        var token = RefreshToken.Issue(UserId, "hash-1", Now);

        token.IsActive(token.ExpiresAt.AddTicks(-1)).ShouldBeTrue();
        token.IsActive(token.ExpiresAt).ShouldBeFalse();
    }

    [Fact]
    public void Revoke_ActiveToken_RecordsReasonAndTime()
    {
        var token = RefreshToken.Issue(UserId, "hash-1", Now);

        token.Revoke(RefreshTokenRevocationReason.Logout, Now);

        token.IsActive(Now).ShouldBeFalse();
        token.RevokedReason.ShouldBe(RefreshTokenRevocationReason.Logout);
        token.RevokedAt.ShouldBe(Now);
        token.ReplacedByTokenId.ShouldBeNull();
    }

    [Fact]
    public void Revoke_AlreadyRevokedToken_KeepsTheFirstReason()
    {
        var token = RefreshToken.Issue(UserId, "hash-1", Now);
        token.Rotate("hash-2", Now);

        token.Revoke(RefreshTokenRevocationReason.ReuseDetected, Now.AddMinutes(1));

        // Revoking a family must not erase the history of which token was rotated.
        token.RevokedReason.ShouldBe(RefreshTokenRevocationReason.Rotated);
        token.RevokedAt.ShouldBe(Now);
    }

    [Fact]
    public void Revoke_WithRotatedReason_Throws()
    {
        var token = RefreshToken.Issue(UserId, "hash-1", Now);

        Should.Throw<ArgumentException>(() => token.Revoke(RefreshTokenRevocationReason.Rotated, Now));
    }
}
