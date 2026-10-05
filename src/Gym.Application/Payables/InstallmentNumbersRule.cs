using FluentValidation;
using FluentValidation.Results;

using Gym.Domain.Payables;

namespace Gym.Application.Payables;

/// <summary>
/// «قسط n از N» checked across three fields at once, through
/// <see cref="Payable.CheckInstallmentNumbers"/>, and reported on the field the Owner has to fix.
/// </summary>
public static class InstallmentNumbersRule
{
    public static void AddInstallmentNumbersRule<T>(
        this AbstractValidator<T> validator,
        Func<T, PayableKind> kind,
        Func<T, int?> installmentNumber,
        Func<T, int?> installmentCount)
    {
        ArgumentNullException.ThrowIfNull(validator);

        validator.RuleFor(command => command).Custom((command, context) =>
        {
            var error = Payable.CheckInstallmentNumbers(
                kind(command), installmentNumber(command), installmentCount(command));
            if (error is null)
            {
                return;
            }

            var field = error == PayableErrors.InstallmentCountOutOfRange ? "InstallmentCount" : "InstallmentNumber";
            context.AddFailure(new ValidationFailure(field, error.Description) { ErrorCode = error.Code });
        });
    }
}
