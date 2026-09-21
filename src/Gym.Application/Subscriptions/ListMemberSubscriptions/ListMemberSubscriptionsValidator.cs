using FluentValidation;

using Gym.Application.Common.Paging;

namespace Gym.Application.Subscriptions.ListMemberSubscriptions;

public sealed class ListMemberSubscriptionsValidator : AbstractValidator<ListMemberSubscriptionsQuery>
{
    public ListMemberSubscriptionsValidator()
    {
        RuleFor(query => query.Page).ValidPage();
        RuleFor(query => query.PageSize).ValidPageSize();
    }
}
