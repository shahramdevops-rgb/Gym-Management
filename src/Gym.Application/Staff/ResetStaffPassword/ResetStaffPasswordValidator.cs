using FluentValidation;

using Gym.Application.Common.Security;

namespace Gym.Application.Staff.ResetStaffPassword;

public sealed class ResetStaffPasswordValidator : AbstractValidator<ResetStaffPasswordCommand>
{
    public ResetStaffPasswordValidator()
    {
        RuleFor(command => command.TemporaryPassword).ValidNewPassword("Staff.TemporaryPasswordRequired");
    }
}
