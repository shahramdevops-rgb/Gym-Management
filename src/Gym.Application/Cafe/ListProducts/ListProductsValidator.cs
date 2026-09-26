using FluentValidation;

using Gym.Application.Common.Paging;

namespace Gym.Application.Cafe.ListProducts;

public sealed class ListProductsValidator : AbstractValidator<ListProductsQuery>
{
    public ListProductsValidator()
    {
        RuleFor(query => query.Page).ValidPage();
        RuleFor(query => query.PageSize).ValidPageSize();
    }
}
