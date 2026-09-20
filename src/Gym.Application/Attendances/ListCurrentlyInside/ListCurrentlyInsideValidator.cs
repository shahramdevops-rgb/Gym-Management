using FluentValidation;

using Gym.Application.Common.Paging;

namespace Gym.Application.Attendances.ListCurrentlyInside;

public sealed class ListCurrentlyInsideValidator : AbstractValidator<ListCurrentlyInsideQuery>
{
    public ListCurrentlyInsideValidator()
    {
        RuleFor(query => query.Page).ValidPage();
        RuleFor(query => query.PageSize).ValidPageSize();
    }
}
