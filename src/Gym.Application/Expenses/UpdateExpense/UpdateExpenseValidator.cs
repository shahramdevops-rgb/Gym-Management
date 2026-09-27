using FluentValidation;

namespace Gym.Application.Expenses.UpdateExpense;

public sealed class UpdateExpenseValidator : AbstractValidator<UpdateExpenseCommand>
{
    public UpdateExpenseValidator()
    {
        RuleFor(command => command.Amount).ValidAmount();
        RuleFor(command => command.CategoryId).ValidCategoryId();
        RuleFor(command => command.Description).ValidDescription();
        RuleFor(command => command.ReferenceNumber).ValidReferenceNumber();
    }
}
