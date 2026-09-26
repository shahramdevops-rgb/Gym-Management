using Gym.Domain.Common;

namespace Gym.Domain.Auth;

/// <summary>
/// A browser a user has logged in from successfully, identified by the secret in its
/// <c>gym_device</c> cookie and stored only as that secret's hash (BUSINESS_RULES.md §1 *Lockout*,
/// docs/adr/0004-password-policy-and-lockout.md).
/// </summary>
/// <remarks>
/// <para>
/// A trusted device has its own count of wrong passwords, apart from the user's Identity lockout,
/// which covers every device the user has not logged in from. Someone on the internet guessing the
/// password therefore locks only the "unknown devices" door, and the front-desk PC keeps working.
/// </para>
/// <para>
/// One row per user and device: a shared front-desk PC has one cookie and one row for each person
/// who has logged in on it. <see cref="UserId"/> is a plain id, like on <see cref="RefreshToken"/>
/// (ADR 0002); the foreign key is enforced by the database.
/// </para>
/// </remarks>
public sealed class TrustedDevice : Entity
{
    /// <summary>BUSINESS_RULES.md §1: trust lasts 90 days from the last successful login on the device.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(90);

    // For EF Core, which materializes rows through a parameterless constructor.
    private TrustedDevice()
    {
    }

    private TrustedDevice(Guid userId, string tokenHash, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        UserId = userId;
        TokenHash = tokenHash;
        LastUsedAt = now;
        ExpiresAt = now + Lifetime;
    }

    public Guid UserId { get; private set; }

    /// <summary>SHA-256 of the cookie's secret, as for refresh tokens: a copy of the database trusts nothing.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset LastUsedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>
    /// Consecutive wrong passwords on this device. Counted with one SQL UPDATE rather than through
    /// this entity, for the same reason as the user's own counter: ten wrong passwords sent at once
    /// must all count (see <c>UserAuthenticator</c>).
    /// </summary>
    public int FailedAttempts { get; private set; }

    /// <summary>Set when <see cref="FailedAttempts"/> reaches the limit; the device is locked until then.</summary>
    public DateTimeOffset? LockedUntil { get; private set; }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public bool IsLockedOut(DateTimeOffset now) => LockedUntil > now;

    /// <summary>Called after the first successful login of this user on this device.</summary>
    public static TrustedDevice Trust(Guid userId, string tokenHash, DateTimeOffset now) => new(userId, tokenHash, now);

    /// <summary>
    /// Another successful login on this device: the 90 days start again, and so does the count of
    /// wrong passwords. An expired device becomes trusted again the same way.
    /// </summary>
    public void Use(DateTimeOffset now)
    {
        Touch(now);
        ClearLockout();
    }

    /// <summary>
    /// The app was used on this device without a password (a session refresh): the 90 days start
    /// again, but the count of wrong passwords stays, because nothing proved the password.
    /// </summary>
    public void Touch(DateTimeOffset now)
    {
        LastUsedAt = now;
        ExpiresAt = now + Lifetime;
    }

    /// <summary>
    /// The browser gets a fresh secret on every login and refresh, so a copy of the old cookie
    /// stops working the next time the real browser is used.
    /// </summary>
    public void ChangeToken(string tokenHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        TokenHash = tokenHash;
    }

    /// <summary>The Owner's unlock, a reset, or the server console: the device may try again.</summary>
    public void ClearLockout()
    {
        FailedAttempts = 0;
        LockedUntil = null;
    }
}
