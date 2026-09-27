using FluentValidation;

namespace Gym.Application.Expenses.RecordExpense;

public sealed class RecordExpenseValidator : AbstractValidator<RecordExpenseCommand>
{
    public RecordExpenseValidator()
    {
        RuleFor(command => command.Amount).ValidAmount();
        RuleFor(command => command.CategoryId).ValidCategoryId();
        RuleFor(command => command.Description).ValidDescription();
        RuleFor(command => command.ReferenceNumber).ValidReferenceNumber();
    }
}
