using FluentValidation;
using FluentValidation.Results;

using Gym.Domain.Auth;
using Gym.Domain.Common.Text;

namespace Gym.Application.Common.Security;

/// <summary>
/// <see cref="PasswordPolicy"/> as one reusable FluentValidation rule, for every command that
/// sets a password: change password, create staff, reset a staff password.
/// </summary>
public static class PasswordRules
{
    /// <summary>
    /// Reports the first rule the password breaks, with that rule's own error code, so the form
    /// shows the one thing to fix. Digits are converted first, exactly as the handler will do
    /// before saving (BUSINESS_RULES.md §1).
    /// </summary>
    /// <param name="userName">
    /// The account's user name, when the command carries it. When it does not (change password,
    /// reset), Identity's validator checks the user name at save time instead.
    /// </param>
    public static IRuleBuilderOptionsConditions<T, string> ValidNewPassword<T>(
        this IRuleBuilderInitial<T, string> rule,
        string requiredCode,
        Func<T, string?>? userName = null) =>
        rule.Custom((password, context) =>
        {
            if (string.IsNullOrEmpty(password))
            {
                context.AddFailure(new ValidationFailure(context.PropertyPath, "Password is required.")
                {
                    ErrorCode = requiredCode,
                });
                return;
            }

            var checkedPassword = PasswordPolicy.Check(
                PersianText.NormalizeDigits(password),
                userName?.Invoke(context.InstanceToValidate));

            if (checkedPassword.IsFailure)
            {
                context.AddFailure(new ValidationFailure(context.PropertyPath, checkedPassword.Error.Description)
                {
                    ErrorCode = checkedPassword.Error.Code,
                });
            }
        });
}
