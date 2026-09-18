using FluentValidation;

using Gym.Application.Common.Paging;

namespace Gym.Application.Staff.ListStaff;

public sealed class ListStaffValidator : AbstractValidator<ListStaffQuery>
{
    public ListStaffValidator()
    {
        RuleFor(query => query.Page).ValidPage();
        RuleFor(query => query.PageSize).ValidPageSize();
    }
}
