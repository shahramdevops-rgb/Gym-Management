using Gym.Application.Auth;
using Gym.Application.Common.Security;
using Gym.Domain.Auth;
using Gym.Domain.Common;
using Gym.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Gym.Infrastructure.Identity;

/// <summary>
/// Checks credentials with Identity's <see cref="UserManager{TUser}"/>, following the order in
/// BUSINESS_RULES.md §1.
/// </summary>
/// <remarks>
/// <para>
/// <c>SignInManager.CheckPasswordSignInAsync</c> does most of this in one call, but
/// <c>SignInManager</c> depends on cookie authentication, which this API does not use. The
/// steps are few enough to write out, and writing them out makes the order visible.
/// </para>
/// <para>
/// The order is the security property:
/// </para>
/// <list type="number">
/// <item>An unknown user name gives the same error as a wrong password, and takes as long.</item>
/// <item>A locked account is refused before the password is checked, so a correct guess
/// during the lockout does not work and does not reveal itself.</item>
/// <item>A wrong password counts toward lockout, atomically (see <see cref="RecordFailedAttemptAsync"/>).</item>
/// <item>Only after the correct password does an inactive account learn that it is inactive.</item>
/// </list>
/// <para>
/// Lockout has two doors (BUSINESS_RULES.md §1 *Lockout*, ADR 0004). A device this user has logged
/// in from before (a <see cref="TrustedDevice"/>) counts wrong passwords on its own row
/// and locks only itself. Every other device shares the user's Identity counter and lockout. So an
/// attacker without the device cookie can lock only the door they are standing at.
/// </para>
/// </remarks>
public sealed class UserAuthenticator(
    UserManager<User> userManager,
    AppDbContext db,
    IOptions<IdentityOptions> identityOptions,
    TimeProvider timeProvider) : IUserAuthenticator
{
    /// <summary>
    /// A real password hash of a random value, made once with the app's own hasher and settings.
    /// Checked against for unknown user names, so they cost the same hashing time as known ones.
    /// </summary>
    private static readonly User TimingUser = new("timing-equalizer", "-");
    private static string? timingHash;

    public async Task<Result<AuthenticatedUser>> AuthenticateAsync(
        string userName,
        string password,
        string? deviceTokenHash,
        CancellationToken cancellationToken)
    {
        // UserManager's methods take no CancellationToken; checking once up front at least
        // avoids starting the work for a request that has already gone away.
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByNameAsync(userName);
        if (user is null)
        {
            // The same error as a wrong password is not enough on its own: returning at once,
            // without the deliberately slow hash, would let response times tell an attacker
            // which user names exist.
            timingHash ??= userManager.PasswordHasher.HashPassword(TimingUser, Guid.NewGuid().ToString());
            userManager.PasswordHasher.VerifyHashedPassword(TimingUser, timingHash, password);

            return Result.Failure<AuthenticatedUser>(AuthErrors.InvalidCredentials);
        }

        var device = await FindTrustedDeviceAsync(user.Id, deviceTokenHash, cancellationToken);

        if (await IsLockedOutAsync(user, device))
        {
            return Result.Failure<AuthenticatedUser>(AuthErrors.LockedOut);
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            // The attempt that reaches the limit already reports the lockout, so the user is not
            // told "wrong password" and then refused with the right one.
            var lockedOut = await RecordFailedAttemptAsync(user.Id, device, cancellationToken);

            return Result.Failure<AuthenticatedUser>(lockedOut ? AuthErrors.LockedOut : AuthErrors.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            return Result.Failure<AuthenticatedUser>(AuthErrors.UserInactive);
        }

        // "5 consecutive wrong passwords": a success starts the count again.
        await ResetFailedAttemptsAsync(user, device, cancellationToken);

        return await ToAuthenticatedUserAsync(user);
    }

    /// <remarks>
    /// Through <see cref="UserManager{TUser}"/>, so the audit log shows why the user was sent to
    /// change their password. If an unrelated edit of the same user wins a concurrency check at
    /// that moment, the flag is set with one plain UPDATE instead: a security flag must never be
    /// lost, and a login must not fail over it.
    /// </remarks>
    public async Task RequirePasswordChangeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await FindExistingAsync(userId);
        user.RequirePasswordChange();

        if ((await userManager.UpdateAsync(user)).Succeeded)
        {
            return;
        }

        await db.Users
            .Where(row => row.Id == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.MustChangePassword, true), cancellationToken);
    }

    public async Task<Result<AuthenticatedUser>> GetActiveUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await FindExistingAsync(userId);
        if (!user.IsActive)
        {
            return Result.Failure<AuthenticatedUser>(AuthErrors.UserInactive);
        }

        return await ToAuthenticatedUserAsync(user);
    }

    public async Task<Result> VerifyPasswordAsync(
        Guid userId,
        string password,
        string? deviceTokenHash,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // The same order as login, except that the caller is already logged in, so saying
        // "inactive" before the password check reveals nothing they do not already know.
        var user = await FindExistingAsync(userId);
        if (!user.IsActive)
        {
            return Result.Failure(AuthErrors.UserInactive);
        }

        var device = await FindTrustedDeviceAsync(user.Id, deviceTokenHash, cancellationToken);

        if (await IsLockedOutAsync(user, device))
        {
            return Result.Failure(AuthErrors.LockedOut);
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            // Counted like a failed login: otherwise a stolen access token would allow
            // unlimited guessing of the password through this endpoint.
            var lockedOut = await RecordFailedAttemptAsync(user.Id, device, cancellationToken);

            return Result.Failure(lockedOut ? AuthErrors.LockedOut : AuthErrors.CurrentPasswordIncorrect);
        }

        await ResetFailedAttemptsAsync(user, device, cancellationToken);

        return Result.Success();
    }

    public async Task<Result<AuthenticatedUser>> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await FindExistingAsync(userId);

        // Set before the call so that UserManager saves the flag in the same UPDATE as the new
        // hash and security stamp. If Identity refuses the password, nothing is saved, and the
        // caller's transaction is rolled back anyway.
        user.PasswordChanged();

        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            return Result.Failure<AuthenticatedUser>(PasswordPolicyValidator.PolicyError(result)
                ?? AuthErrors.PasswordRejected(string.Join(" ", result.Errors.Select(error => error.Description))));
        }

        return await ToAuthenticatedUserAsync(user);
    }

    /// <summary>
    /// The device this request came from, if it is trusted for this user and still in date;
    /// otherwise null, and the request is at the "unknown devices" door.
    /// </summary>
    private async Task<TrustedDevice?> FindTrustedDeviceAsync(
        Guid userId,
        string? deviceTokenHash,
        CancellationToken cancellationToken)
    {
        if (deviceTokenHash is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();

        return await db.TrustedDevices
            .AsNoTracking()
            .SingleOrDefaultAsync(
                device => device.UserId == userId && device.TokenHash == deviceTokenHash && device.ExpiresAt > now,
                cancellationToken);
    }

    /// <summary>A trusted device is locked only by its own count; any other device by the user's.</summary>
    private async Task<bool> IsLockedOutAsync(User user, TrustedDevice? device) =>
        device is null
            ? await userManager.IsLockedOutAsync(user)
            : device.IsLockedOut(timeProvider.GetUtcNow());

    /// <summary>
    /// Counts one failed attempt at the door the request came through, and returns whether that
    /// door is locked afterwards.
    /// </summary>
    private async Task<bool> RecordFailedAttemptAsync(Guid userId, TrustedDevice? device, CancellationToken cancellationToken) =>
        device is null
            ? await RecordUserFailedAttemptAsync(userId, cancellationToken)
            : await RecordDeviceFailedAttemptAsync(device.Id, cancellationToken)
                // The device was forgotten a moment ago (a reset, a password change elsewhere),
                // so this attempt came from what is now an unknown device, and counts there.
                ?? await RecordUserFailedAttemptAsync(userId, cancellationToken);

    /// <summary>
    /// Counts one failed attempt from an unknown device and locks that door when it reaches the
    /// limit, in a single SQL UPDATE.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not <c>UserManager.AccessFailedAsync</c>: that reads the count, adds one in C#, and saves
    /// with a concurrency check. Ten wrong passwords sent at the same moment all read the same
    /// count, one save wins and nine fail. Lockout would count one attempt where ten were made,
    /// and the losers would get a 500.
    /// </para>
    /// <para>
    /// Here Postgres does the arithmetic on the row it has locked for the update, so parallel
    /// attempts queue behind each other and every one of them counts. The rules mirror Identity's
    /// own: at the limit, set the lockout end and start the count again. The concurrency stamp is
    /// left alone on purpose, because a counter is not an edit, and changing the stamp would make
    /// an Owner's unrelated update of this user fail. Being a bulk update, it bypasses the audit
    /// interceptor, which is also deliberate: failed-attempt counters are bookkeeping, not changes
    /// anyone reviews.
    /// </para>
    /// </remarks>
    private async Task<bool> RecordUserFailedAttemptAsync(Guid userId, CancellationToken cancellationToken)
    {
        var (limit, now, lockoutEnd) = LockoutRule();

        await db.Users
            .Where(user => user.Id == userId && user.LockoutEnabled)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(user => user.LockoutEnd, user => user.AccessFailedCount + 1 >= limit ? lockoutEnd : user.LockoutEnd)
                    .SetProperty(user => user.AccessFailedCount, user => user.AccessFailedCount + 1 >= limit ? 0 : user.AccessFailedCount + 1),
                cancellationToken);

        var currentLockoutEnd = await db.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.LockoutEnd)
            .SingleAsync(cancellationToken);

        return currentLockoutEnd > now;
    }

    /// <summary>
    /// The same count and the same limit as <see cref="RecordUserFailedAttemptAsync"/>, kept on
    /// the trusted device's own row, so it locks that device and nothing else. Null when the row no
    /// longer exists.
    /// </summary>
    private async Task<bool?> RecordDeviceFailedAttemptAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        var (limit, now, lockoutEnd) = LockoutRule();

        var updated = await db.TrustedDevices
            .Where(device => device.Id == deviceId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(device => device.LockedUntil, device => device.FailedAttempts + 1 >= limit ? lockoutEnd : device.LockedUntil)
                    .SetProperty(device => device.FailedAttempts, device => device.FailedAttempts + 1 >= limit ? 0 : device.FailedAttempts + 1),
                cancellationToken);

        if (updated == 0)
        {
            return null;
        }

        var lockedUntil = await db.TrustedDevices
            .AsNoTracking()
            .Where(device => device.Id == deviceId)
            .Select(device => device.LockedUntil)
            .SingleOrDefaultAsync(cancellationToken);

        return lockedUntil > now;
    }

    /// <summary>BUSINESS_RULES.md §1, configured once in <c>AddInfrastructure</c>: 5 attempts, 15 minutes.</summary>
    private (int Limit, DateTimeOffset Now, DateTimeOffset? LockoutEnd) LockoutRule()
    {
        var lockout = identityOptions.Value.Lockout;
        var now = timeProvider.GetUtcNow();

        return (lockout.MaxFailedAccessAttempts, now, now + lockout.DefaultLockoutTimeSpan);
    }

    /// <summary>
    /// Back to zero after a correct password, at the door it came through and at the unknown
    /// devices' door too: the real user has just proved themselves, so an attacker's half-finished
    /// count need not stand. A lockout already in force is left alone. Atomic for the same reason as
    /// <see cref="RecordUserFailedAttemptAsync"/>: two correct logins at once must not make one of
    /// them fail on a concurrency check.
    /// </summary>
    private async Task ResetFailedAttemptsAsync(User user, TrustedDevice? device, CancellationToken cancellationToken)
    {
        if (user.AccessFailedCount > 0)
        {
            await db.Users
                .Where(row => row.Id == user.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.AccessFailedCount, 0), cancellationToken);
        }

        if (device is { FailedAttempts: > 0 })
        {
            await db.TrustedDevices
                .Where(row => row.Id == device.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.FailedAttempts, 0), cancellationToken);
        }
    }

    /// <summary>
    /// Every caller holds an id from a token this API issued or a row with a restricting
    /// foreign key, and users are never deleted, so a missing user is a broken invariant.
    /// </summary>
    private async Task<User> FindExistingAsync(Guid userId) =>
        await userManager.FindByIdAsync(userId.ToString())
        ?? throw new InvalidOperationException($"User {userId} does not exist.");

    private async Task<AuthenticatedUser> ToAuthenticatedUserAsync(User user)
    {
        var roles = await userManager.GetRolesAsync(user);

        return new AuthenticatedUser(user.Id, user.UserName!, user.FullName, [.. roles], user.MustChangePassword);
    }
}
