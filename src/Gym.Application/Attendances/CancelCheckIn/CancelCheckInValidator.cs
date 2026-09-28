using FluentValidation;

using Gym.Domain.Attendances;

namespace Gym.Application.Attendances.CancelCheckIn;

/// <summary>
/// Both choices must be present (BUSINESS_RULES.md §7 <i>Cancel check-in</i>: the server does not
/// guess), and an order is named at most once.
/// </summary>
public sealed class CancelCheckInValidator : AbstractValidator<CancelCheckInCommand>
{
    public CancelCheckInValidator()
    {
        RuleFor(command => command.VoidCardio)
            .NotNull()
            .WithErrorCode(AttendanceErrors.CancelChoiceRequired.Code)
            .WithMessage(AttendanceErrors.CancelChoiceRequired.Description);

        RuleFor(command => command.CafeOrderIds)
            .NotNull()
            .WithErrorCode(AttendanceErrors.CancelChoiceRequired.Code)
            .WithMessage(AttendanceErrors.CancelChoiceRequired.Description);

        RuleFor(command => command.CafeOrderIds)
            .Must(ids => ids!.Distinct().Count() == ids!.Count)
            .When(command => command.CafeOrderIds is not null)
            .WithErrorCode(AttendanceErrors.CancelChoiceRequired.Code)
            .WithMessage("A cafe order is named more than once.");
    }
}
