using FluentValidation;

using Gym.Application.Common.Paging;

namespace Gym.Application.Expenses.ListExpenseCategories;

public sealed class ListExpenseCategoriesValidator : AbstractValidator<ListExpenseCategoriesQuery>
{
    public ListExpenseCategoriesValidator()
    {
        RuleFor(query => query.Page).ValidPage();
        RuleFor(query => query.PageSize).ValidPageSize();
    }
}
