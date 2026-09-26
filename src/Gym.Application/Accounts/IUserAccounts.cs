using Gym.Domain.Common;

namespace Gym.Application.Accounts;

/// <summary>
/// Any account, the Owner's included, for the server console (<c>./server.sh unlock</c>,
/// <c>set-password</c>, <c>rename</c>). Declared here and implemented in Infrastructure with
/// Identity, because Application cannot see the Identity <c>User</c> (ADR 0002).
/// </summary>
/// <remarks>
/// The web API never reaches these: the Owner's screens go through <c>IStaffAccounts</c>, which
/// cannot touch the Owner. The console is for the day the Owner is the one locked out, and it
/// takes an SSH key to the server to run it.
/// </remarks>
public interface IUserAccounts
{
    Task<Guid?> FindIdByUserNameAsync(string userName, CancellationToken cancellationToken);

    /// <summary>Clears the lockout at both doors (BUSINESS_RULES.md §1 *Lockout*).</summary>
    Task<Result> UnlockAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Sets a password the person typed themselves on the server: it follows the password policy,
    /// needs no change at the next login, and clears any lockout. Sessions are the caller's job.
    /// </summary>
    Task<Result> SetPasswordAsync(Guid userId, string password, CancellationToken cancellationToken);

    Task<Result> RenameAsync(Guid userId, string newUserName, CancellationToken cancellationToken);
}
