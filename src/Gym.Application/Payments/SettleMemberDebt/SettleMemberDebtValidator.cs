using FluentValidation;

using Gym.Domain.Payments;

namespace Gym.Application.Payments.SettleMemberDebt;

public sealed class SettleMemberDebtValidator : AbstractValidator<SettleMemberDebtCommand>
{
    public SettleMemberDebtValidator()
    {
        RuleFor(command => command.Amount).ValidAmount();
        RuleFor(command => command.Method).ValidMethod();
        RuleFor(command => command.ReferenceNumber).ValidReferenceNumber();

        RuleFor(command => command.Items)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode(SettlementErrors.NoItems.Code).WithMessage(SettlementErrors.NoItems.Description)
            .Must(items => items.Select(item => item.Id).Distinct().Count() == items.Count)
            .WithErrorCode(SettlementErrors.DuplicateItem.Code).WithMessage(SettlementErrors.DuplicateItem.Description);
    }
}
