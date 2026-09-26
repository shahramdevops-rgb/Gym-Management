using FluentValidation;

namespace Gym.Application.Cafe.CancelCafeOrder;

public sealed class CancelCafeOrderValidator : AbstractValidator<CancelCafeOrderCommand>
{
    public CancelCafeOrderValidator()
    {
        RuleFor(command => command.Reason).ValidCancelReason();
    }
}
