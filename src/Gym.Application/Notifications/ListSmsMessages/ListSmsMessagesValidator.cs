using FluentValidation;

using Gym.Application.Common.Paging;
using Gym.Domain.Notifications;

namespace Gym.Application.Notifications.ListSmsMessages;

public sealed class ListSmsMessagesValidator : AbstractValidator<ListSmsMessagesQuery>
{
    public ListSmsMessagesValidator()
    {
        RuleFor(query => query.Page).ValidPage();
        RuleFor(query => query.PageSize).ValidPageSize();

        RuleFor(query => query)
            .Must(query => query.From is null || query.To is null || query.From <= query.To)
            .WithErrorCode(NotificationErrors.InvalidDateRange.Code)
            .WithMessage(NotificationErrors.InvalidDateRange.Description)
            .OverridePropertyName(nameof(ListSmsMessagesQuery.To));
    }
}
