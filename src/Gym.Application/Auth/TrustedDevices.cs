using Gym.Application.Common;
using Gym.Application.Common.Security;
using Gym.Domain.Auth;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Auth;

/// <summary>
/// Trusting and forgetting devices (BUSINESS_RULES.md §1 *Lockout*, ADR 0004). None of these
/// save: the caller saves as part of its own unit of work, like <see cref="RefreshTokenRevocation"/>.
/// </summary>
/// <remarks>
/// <para>
/// The cookie's secret is the same kind of value as a refresh token, 32 random bytes stored only as
/// a SHA-256 hash, so it is made and hashed by <see cref="RefreshTokenSecret"/>.
/// </para>
/// <para>
/// The secret is replaced on every successful login and refresh, for every row that shares it (a
/// front-desk PC has one cookie and a row for each person who logs in on it). A copy of the cookie,
/// taken from the browser's developer tools say, stops working the next time the real browser is
/// used, instead of letting its holder count wrong passwords on that PC's door from elsewhere.
/// </para>
/// </remarks>
public static class TrustedDevices
{
    /// <summary>The hash a presented cookie is looked up by; null when there is no cookie.</summary>
    public static string? HashOf(string? deviceToken) =>
        string.IsNullOrEmpty(deviceToken) ? null : RefreshTokenSecret.Hash(deviceToken);

    /// <summary>
    /// After a successful login: trusts this browser for the user, moves every row that shared the
    /// old secret to a new one, and returns the new secret for the cookie.
    /// </summary>
    /// <remarks>
    /// A secret the app never issued matches no row, so nothing carries over from it: a value
    /// someone planted in the cookie never becomes a trusted device.
    /// </remarks>
    public static async Task<string> TrustDeviceAsync(
        this IAppDbContext db,
        Guid userId,
        string? presentedToken,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        var (token, tokenHash, rows) = await RotateAsync(db, HashOf(presentedToken), cancellationToken);

        var device = rows.SingleOrDefault(row => row.UserId == userId);
        if (device is null)
        {
            db.TrustedDevices.Add(TrustedDevice.Trust(userId, tokenHash, now));
        }
        else
        {
            device.Use(now);
        }

        return token;
    }

    /// <summary>
    /// After a successful refresh: if this browser is trusted for the user and still in date, its
    /// 90 days start again and it gets a new secret, returned for the cookie. Otherwise nothing
    /// changes and the result is null: a refresh proves a session, not a password, so it never
    /// trusts a browser that was not trusted already.
    /// </summary>
    public static async Task<string?> RenewDeviceAsync(
        this IAppDbContext db,
        Guid userId,
        string? presentedToken,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        var presentedHash = HashOf(presentedToken);
        if (presentedHash is null ||
            !await db.TrustedDevices.AnyAsync(
                row => row.UserId == userId && row.TokenHash == presentedHash && row.ExpiresAt > now,
                cancellationToken))
        {
            return null;
        }

        var (token, _, rows) = await RotateAsync(db, presentedHash, cancellationToken);
        rows.Single(row => row.UserId == userId).Touch(now);

        return token;
    }

    /// <summary>
    /// Stops trusting the user's devices, all of them or all but the one making the request. After
    /// a password change or reset, a device that logged in with the old password, perhaps an
    /// attacker's, must not keep its own door.
    /// </summary>
    public static async Task ForgetDevicesAsync(
        this IAppDbContext db,
        Guid userId,
        string? exceptTokenHash,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        var devices = await db.TrustedDevices
            .Where(device => device.UserId == userId && device.TokenHash != exceptTokenHash)
            .ToListAsync(cancellationToken);

        db.TrustedDevices.RemoveRange(devices);
    }

    /// <summary>A new secret, and every row that held the old one moved onto it.</summary>
    private static async Task<(string Token, string TokenHash, List<TrustedDevice> Rows)> RotateAsync(
        IAppDbContext db,
        string? presentedHash,
        CancellationToken cancellationToken)
    {
        var token = RefreshTokenSecret.Generate();
        var tokenHash = RefreshTokenSecret.Hash(token);

        var rows = presentedHash is null
            ? []
            : await db.TrustedDevices.Where(row => row.TokenHash == presentedHash).ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            row.ChangeToken(tokenHash);
        }

        return (token, tokenHash, rows);
    }
}
