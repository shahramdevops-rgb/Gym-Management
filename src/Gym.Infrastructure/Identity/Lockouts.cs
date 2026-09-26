using Gym.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gym.Infrastructure.Identity;

/// <summary>
/// Undoing a lockout, at both doors (BUSINESS_RULES.md §1 *Lockout*): the user's own lockout,
/// which stops unknown devices, and every trusted device's. Shared by the Owner's unlock and
/// password reset and by the server console, so "unlocked" means the same everywhere.
/// </summary>
internal static class Lockouts
{
    /// <summary>
    /// Clears both, through tracked changes so the audit log records the unlock. The user's part
    /// goes through <see cref="UserManager{TUser}"/> and can fail on a concurrency check, which is
    /// returned rather than thrown; the devices' part runs only if the user's succeeded.
    /// </summary>
    public static async Task<IdentityResult> ClearAsync(
        UserManager<User> userManager,
        AppDbContext db,
        User user,
        CancellationToken cancellationToken)
    {
        var steps = new Func<Task<IdentityResult>>[]
        {
            () => userManager.SetLockoutEndDateAsync(user, null),
            () => userManager.ResetAccessFailedCountAsync(user),
        };

        foreach (var step in steps)
        {
            var result = await step();
            if (!result.Succeeded)
            {
                return result;
            }
        }

        var devices = await db.TrustedDevices
            .Where(device => device.UserId == user.Id && (device.FailedAttempts > 0 || device.LockedUntil != null))
            .ToListAsync(cancellationToken);

        foreach (var device in devices)
        {
            device.ClearLockout();
        }

        await db.SaveChangesAsync(cancellationToken);

        return IdentityResult.Success;
    }
}
