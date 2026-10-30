using FluentValidation;

using Gym.Domain.Timetables;

namespace Gym.Application.Timetables.SetTimetableDay;

/// <summary>Shape only; the sessions' own rules, and which dates can change, are the Domain's.</summary>
public sealed class SetTimetableDayValidator : AbstractValidator<SetTimetableDayCommand>
{
    public SetTimetableDayValidator()
    {
        RuleFor(command => command.Sessions)
            .NotNull()
            .Must(sessions => sessions.Count <= TimetableRules.MaxSessionsPerDay)
            .WithErrorCode(TimetableErrors.TooManySessions.Code).WithMessage(TimetableErrors.TooManySessions.Description);

        RuleForEach(command => command.Sessions).NotNull();
    }
}
