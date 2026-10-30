using FluentValidation;

using Gym.Domain.Timetables;

namespace Gym.Application.Timetables.SetWeekPattern;

/// <summary>Shape only, and a size the request cannot exceed; the sessions' own rules are the Domain's.</summary>
public sealed class SetWeekPatternValidator : AbstractValidator<SetWeekPatternCommand>
{
    public SetWeekPatternValidator()
    {
        RuleFor(command => command.Sessions)
            .NotNull()
            .Must(sessions => sessions.Count <= 7 * TimetableRules.MaxSessionsPerDay)
            .WithErrorCode(TimetableErrors.TooManySessions.Code).WithMessage(TimetableErrors.TooManySessions.Description);

        RuleForEach(command => command.Sessions).NotNull();
    }
}
