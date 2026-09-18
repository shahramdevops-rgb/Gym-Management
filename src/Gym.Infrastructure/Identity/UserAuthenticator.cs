using Gym.Application.Auth;
using Gym.Application.Common.Security;
using Gym.Domain.Common;

using Microsoft.AspNetCore.Identity;

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
/// <item>An unknown user name gives the same error as a wrong password.</item>
/// <item>A locked account is refused before the password is checked, so a correct guess
/// during the lockout does not work and does not reveal itself.</item>
/// <item>A wrong password counts toward lockout.</item>
/// <item>Only after the correct password does an inactive account learn that it is inactive.</item>
/// </list>
/// </remarks>
public sealed class UserAuthenticator(UserManager<User> userManager) : IUserAuthenticator
{
    public async Task<Result<AuthenticatedUser>> AuthenticateAsync(
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        // UserManager's methods take no CancellationToken; checking once up front at least
        // avoids starting the work for a request that has already gone away.
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByNameAsync(userName);
        if (user is null)
        {
            return Result.Failure<AuthenticatedUser>(AuthErrors.InvalidCredentials);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return Result.Failure<AuthenticatedUser>(AuthErrors.LockedOut);
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            ThrowIfFailed(await userManager.AccessFailedAsync(user), "record a failed login");

            // The attempt that reaches the limit already reports the lockout, so the user is not
            // told "wrong password" and then refused with the right one.
            return Result.Failure<AuthenticatedUser>(
                await userManager.IsLockedOutAsync(user) ? AuthErrors.LockedOut : AuthErrors.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            return Result.Failure<AuthenticatedUser>(AuthErrors.UserInactive);
        }

        // "5 consecutive wrong passwords": a success starts the count again.
        if (await userManager.GetAccessFailedCountAsync(user) > 0)
        {
            ThrowIfFailed(await userManager.ResetAccessFailedCountAsync(user), "reset the failed login count");
        }

        var roles = await userManager.GetRolesAsync(user);

        return new AuthenticatedUser(user.Id, user.UserName!, user.FullName, [.. roles], user.MustChangePassword);
    }

    /// <summary>
    /// Identity reports failures as a result, not an exception. Failing to record a login
    /// attempt is unexpected, and silently ignoring it would switch lockout off.
    /// </summary>
    private static void ThrowIfFailed(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not {action}: {string.Join("; ", result.Errors.Select(error => error.Description))}");
        }
    }
}
