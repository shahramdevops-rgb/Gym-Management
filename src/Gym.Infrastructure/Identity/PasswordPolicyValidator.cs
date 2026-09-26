using Gym.Domain.Auth;
using Gym.Domain.Common;

using Microsoft.AspNetCore.Identity;

namespace Gym.Infrastructure.Identity;

/// <summary>
/// Identity's side of <see cref="PasswordPolicy"/>: every password Identity stores passes it,
/// whichever code path set it (create staff, reset, change, the seeded Owner, the server console).
/// </summary>
/// <remarks>
/// Identity's own composition options (<c>RequireUppercase</c> and the rest) are switched off in
/// <c>AddInfrastructure</c>: the policy deliberately has none (BUSINESS_RULES.md §1). The error
/// code is the policy's own, such as <c>Auth.PasswordContainsUserName</c>, so a refusal found
/// here reaches the frontend with the same code the form would have shown.
/// </remarks>
public sealed class PasswordPolicyValidator : IPasswordValidator<User>
{
    public Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user, string? password)
    {
        ArgumentNullException.ThrowIfNull(user);

        var result = PasswordPolicy.Check(password, user.UserName);

        return Task.FromResult(result.IsSuccess
            ? IdentityResult.Success
            : IdentityResult.Failed(new IdentityError { Code = result.Error.Code, Description = result.Error.Description }));
    }

    /// <summary>The policy error inside a failed Identity result, if the refusal was the policy's.</summary>
    public static Error? PolicyError(IdentityResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return PasswordErrors.All.FirstOrDefault(error => result.Errors.Any(identityError => identityError.Code == error.Code));
    }
}
