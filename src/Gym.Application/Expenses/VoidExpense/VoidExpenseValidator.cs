using FluentValidation;

namespace Gym.Application.Expenses.VoidExpense;

public sealed class VoidExpenseValidator : AbstractValidator<VoidExpenseCommand>
{
    public VoidExpenseValidator()
    {
        RuleFor(command => command.Reason).ValidVoidReason();
    }
}
