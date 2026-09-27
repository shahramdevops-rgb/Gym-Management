using FluentValidation;

namespace Gym.Application.Expenses.CreateExpenseCategory;

public sealed class CreateExpenseCategoryValidator : AbstractValidator<CreateExpenseCategoryCommand>
{
    public CreateExpenseCategoryValidator()
    {
        RuleFor(command => command.Name).ValidCategoryName();
    }
}
