using FluentValidation;

using Gym.Application.Common.Paging;
using Gym.Domain.Expenses;

namespace Gym.Application.Expenses.ListExpenses;

public sealed class ListExpensesValidator : AbstractValidator<ListExpensesQuery>
{
    public ListExpensesValidator()
    {
        RuleFor(query => query.Page).ValidPage();
        RuleFor(query => query.PageSize).ValidPageSize();

        RuleFor(query => query)
            .Must(query => query.From is null || query.To is null || query.From <= query.To)
            .WithErrorCode(ExpenseErrors.InvalidDateRange.Code)
            .WithMessage(ExpenseErrors.InvalidDateRange.Description)
            .OverridePropertyName(nameof(ListExpensesQuery.To));
    }
}
