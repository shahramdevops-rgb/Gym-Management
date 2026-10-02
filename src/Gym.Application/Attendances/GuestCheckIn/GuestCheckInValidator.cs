using FluentValidation;

using Gym.Domain.Attendances;

namespace Gym.Application.Attendances.GuestCheckIn;

/// <summary>
/// The name's checks, under the entity's error codes, so the name step of the box shows them under
/// its field before anything touches the database (the same split as <c>MemberRules</c>).
/// </summary>
public sealed class GuestCheckInValidator : AbstractValidator<GuestCheckInCommand>
{
    public GuestCheckInValidator()
    {
        RuleFor(command => command.GuestName)
            .Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithErrorCode(AttendanceErrors.GuestNameRequired.Code).WithMessage(AttendanceErrors.GuestNameRequired.Description)
            .Must(name => name.Trim().Length <= Attendance.GuestNameMaxLength)
            .WithErrorCode(AttendanceErrors.GuestNameTooLong.Code).WithMessage(AttendanceErrors.GuestNameTooLong.Description);
    }
}
