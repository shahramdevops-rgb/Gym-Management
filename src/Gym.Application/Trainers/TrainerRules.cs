using FluentValidation;

using Gym.Domain.Trainers;

namespace Gym.Application.Trainers;

/// <summary>
/// The name check create and update share, with the entity's error codes. The phone is checked as a
/// member's is (<c>MemberRules.ValidPhoneInput</c>), and its format by the handler through
/// <c>IPhoneNormalizer</c>.
/// </summary>
public static class TrainerRules
{
    public static IRuleBuilderOptions<T, string> ValidTrainerName<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithErrorCode(TrainerErrors.FullNameRequired.Code).WithMessage(TrainerErrors.FullNameRequired.Description)
            .Must(name => name.Trim().Length <= Trainer.FullNameMaxLength)
            .WithErrorCode(TrainerErrors.FullNameTooLong.Code).WithMessage(TrainerErrors.FullNameTooLong.Description);
}
