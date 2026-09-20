using FluentValidation;

using Gym.Application.Common.Paging;

namespace Gym.Application.Lockers.ListLockers;

public sealed class ListLockersValidator : AbstractValidator<ListLockersQuery>
{
    public ListLockersValidator()
    {
        RuleFor(query => query.Page).ValidPage();
        RuleFor(query => query.PageSize).ValidPageSize();
    }
}
