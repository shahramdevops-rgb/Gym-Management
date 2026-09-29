using FluentValidation;

namespace Gym.Application.Lockers.LockerUsage;

public sealed class LockerUsageValidator : AbstractValidator<LockerUsageQuery>
{
    public LockerUsageValidator()
    {
        RuleFor(query => query.Days)
            .Must(days => LockerUsageQuery.AllowedDays.Contains(days))
            .WithMessage("Days must be 7, 30 or 90.");
    }
}
