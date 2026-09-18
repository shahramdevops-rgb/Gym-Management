using FluentValidation;

using Gym.Application.Common.Security;

namespace Gym.Application.Staff.CreateStaff;

public sealed class CreateStaffValidator : AbstractValidator<CreateStaffCommand>
{
    public CreateStaffValidator()
    {
        RuleFor(command => command.UserName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("Staff.UserNameRequired").WithMessage("User name is required.")
            .Length(UserNamePolicy.MinimumLength, UserNamePolicy.MaximumLength).WithErrorCode("Staff.UserNameLength")
                .WithMessage($"User name must be {UserNamePolicy.MinimumLength} to {UserNamePolicy.MaximumLength} characters.")
            .Must(UserNamePolicy.HasOnlyAllowedCharacters).WithErrorCode("Staff.UserNameInvalidCharacters")
                .WithMessage("User name may contain only Latin letters, digits and - . _ @ +.");

        // Checked on the cleaned name, because that is what will be saved.
        RuleFor(command => StaffNames.Clean(command.FullName))
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("Staff.FullNameRequired").WithMessage("Full name is required.")
            .MaximumLength(StaffNames.FullNameMaxLength).WithErrorCode("Staff.FullNameTooLong").WithMessage("Full name is too long.")
            .OverridePropertyName(nameof(CreateStaffCommand.FullName));

        RuleFor(command => command.TemporaryPassword).ValidNewPassword("Staff.TemporaryPasswordRequired");
    }
}
