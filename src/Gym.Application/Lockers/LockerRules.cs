using FluentValidation;

using Gym.Domain.Lockers;

namespace Gym.Application.Lockers;

/// <summary>
/// The field check create shares with the entity's error code, so the form gets a message
/// before anything touches the database.
/// </summary>
public static class LockerRules
{
    public static IRuleBuilderOptions<T, int> ValidNumber<T>(this IRuleBuilderInitial<T, int> rule) =>
        rule.Must(number => Locker.CheckNumber(number) is null)
            .WithErrorCode(LockerErrors.NumberInvalid.Code).WithMessage(LockerErrors.NumberInvalid.Description);
}
