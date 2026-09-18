using FluentValidation;

using Gym.Application.Common.Paging;

namespace Gym.Application.Plans.ListPlans;

public sealed class ListPlansValidator : AbstractValidator<ListPlansQuery>
{
    public ListPlansValidator()
    {
        RuleFor(query => query.Page).ValidPage();
        RuleFor(query => query.PageSize).ValidPageSize();
    }
}
