using FluentValidation;

namespace Gym.Application.Expenses.UpdateExpenseCategory;

public sealed class UpdateExpenseCategoryValidator : AbstractValidator<UpdateExpenseCategoryCommand>
{
    public UpdateExpenseCategoryValidator()
    {
        RuleFor(command => command.Name).ValidCategoryName();
    }
}
