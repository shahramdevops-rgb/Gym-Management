using FluentValidation;

using Gym.Application.Common.Paging;

namespace Gym.Application.Cafe.ListProductCategories;

public sealed class ListProductCategoriesValidator : AbstractValidator<ListProductCategoriesQuery>
{
    public ListProductCategoriesValidator()
    {
        RuleFor(query => query.Page).ValidPage();
        RuleFor(query => query.PageSize).ValidPageSize();
    }
}
