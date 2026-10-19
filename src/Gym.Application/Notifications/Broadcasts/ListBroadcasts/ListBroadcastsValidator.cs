using FluentValidation;

using Gym.Application.Common.Paging;

namespace Gym.Application.Notifications.Broadcasts.ListBroadcasts;

public sealed class ListBroadcastsValidator : AbstractValidator<ListBroadcastsQuery>
{
    public ListBroadcastsValidator()
    {
        RuleFor(query => query.Page).ValidPage();
        RuleFor(query => query.PageSize).ValidPageSize();
    }
}
