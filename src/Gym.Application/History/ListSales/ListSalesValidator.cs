using FluentValidation;

using Gym.Application.Common.Paging;

namespace Gym.Application.History.ListSales;

public sealed class ListSalesValidator : AbstractValidator<ListSalesQuery>
{
    public ListSalesValidator()
    {
        RuleFor(query => query.Page).ValidPage();
        RuleFor(query => query.PageSize).ValidPageSize();

        Include(new SalesFilterValidator());
    }
}
