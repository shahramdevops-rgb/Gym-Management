using FluentValidation;

using Gym.Application.Common.Paging;

namespace Gym.Application.GuestDebts.ListGuestDebts;

public sealed class ListGuestDebtsValidator : AbstractValidator<ListGuestDebtsQuery>
{
    public ListGuestDebtsValidator()
    {
        RuleFor(query => query.Page).ValidPage();
        RuleFor(query => query.PageSize).ValidPageSize();
    }
}
