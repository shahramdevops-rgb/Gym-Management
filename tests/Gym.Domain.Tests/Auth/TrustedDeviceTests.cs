using Gym.Domain.Auth;

namespace Gym.Domain.Tests.Auth;

public sealed class TrustedDeviceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.CreateVersion7();

    [Fact]
    public void Trust_WhenCalled_TrustsTheDeviceForNinetyDays()
    {
        var device = TrustedDevice.Trust(UserId, "hash-1", Now);

        device.UserId.ShouldBe(UserId);
        device.TokenHash.ShouldBe("hash-1");
        device.LastUsedAt.ShouldBe(Now);
        device.ExpiresAt.ShouldBe(Now.AddDays(90));
        device.FailedAttempts.ShouldBe(0);
        device.IsLockedOut(Now).ShouldBeFalse();
    }

    [Fact]
    public void IsExpired_AtTheExactExpiryMoment_ReturnsTrue()
    {
        var device = TrustedDevice.Trust(UserId, "hash-1", Now);

        device.IsExpired(Now.AddDays(90).AddTicks(-1)).ShouldBeFalse();
        device.IsExpired(Now.AddDays(90)).ShouldBeTrue();
    }

    [Fact]
    public void Use_LaterLogin_StartsTheNinetyDaysAgain()
    {
        var device = TrustedDevice.Trust(UserId, "hash-1", Now);
        var later = Now.AddDays(89);

        device.Use(later);

        device.LastUsedAt.ShouldBe(later);
        device.ExpiresAt.ShouldBe(later.AddDays(90));
    }

    [Fact]
    public void Touch_Refresh_RenewsTheNinetyDaysButKeepsTheCount()
    {
        var device = TrustedDevice.Trust(UserId, "hash-1", Now);
        var later = Now.AddDays(30);

        device.Touch(later);

        device.ExpiresAt.ShouldBe(later.AddDays(90));
        device.LastUsedAt.ShouldBe(later);
    }

    [Fact]
    public void ChangeToken_NewHash_ReplacesTheOldOne()
    {
        var device = TrustedDevice.Trust(UserId, "hash-1", Now);

        device.ChangeToken("hash-2");

        device.TokenHash.ShouldBe("hash-2");
        Should.Throw<ArgumentException>(() => device.ChangeToken(""));
    }

    [Fact]
    public void Trust_EmptyHash_Throws()
    {
        Should.Throw<ArgumentException>(() => TrustedDevice.Trust(UserId, " ", Now));
    }
}
