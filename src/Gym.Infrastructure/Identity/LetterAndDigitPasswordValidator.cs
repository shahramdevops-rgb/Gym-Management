using Gym.Application.Common.Security;

using Microsoft.AspNetCore.Identity;

namespace Gym.Infrastructure.Identity;

/// <summary>
/// Requires at least one letter and one digit, with no case or symbol requirement.
/// </summary>
/// <remarks>
/// Identity's own <c>RequireLowercase</c>/<c>RequireUppercase</c>/<c>RequireNonAlphanumeric</c>
/// options are switched off in <c>AddInfrastructure</c> in favor of this single, case-agnostic
/// rule: passwords are typed on a Persian keyboard at the front desk, where hitting a specific
/// case or finding a symbol is error-prone. <c>RequiredLength</c> stays a built-in check.
/// </remarks>
public sealed class LetterAndDigitPasswordValidator : IPasswordValidator<User>
{
    public Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user, string? password)
    {
        var result = PasswordPolicy.HasLetterAndDigit(password)
            ? IdentityResult.Success
            : IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordRequiresLetterAndDigit",
                Description = "Passwords must contain at least one letter and one digit.",
            });

        return Task.FromResult(result);
    }
}
