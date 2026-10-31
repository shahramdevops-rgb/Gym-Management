using FluentValidation;

using Gym.Domain.Bookings;

namespace Gym.Application.Bookings.SetStudentSchedule;

/// <summary>Shape only, and a size the request cannot exceed; the schedule's own rules are the Domain's.</summary>
public sealed class SetStudentScheduleValidator : AbstractValidator<SetStudentScheduleCommand>
{
    public SetStudentScheduleValidator()
    {
        RuleFor(command => command.Slots)
            .NotNull()
            .Must(slots => slots.Count <= BookingRules.MaxSlots)
            .WithErrorCode(BookingErrors.TooManySlots.Code).WithMessage(BookingErrors.TooManySlots.Description);

        RuleForEach(command => command.Slots).NotNull();
    }
}
