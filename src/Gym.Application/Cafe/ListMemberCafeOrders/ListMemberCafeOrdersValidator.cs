using FluentValidation;

using Gym.Application.Common.Paging;
using Gym.Domain.Cafe;

namespace Gym.Application.Cafe.ListMemberCafeOrders;

public sealed class ListMemberCafeOrdersValidator : AbstractValidator<ListMemberCafeOrdersQuery>
{
    public ListMemberCafeOrdersValidator()
    {
        RuleFor(query => query.Page).ValidPage();
        RuleFor(query => query.PageSize).ValidPageSize();

        RuleFor(query => query)
            .Must(query => query.From is null || query.To is null || query.From <= query.To)
            .WithErrorCode(CafeOrderErrors.InvalidDateRange.Code)
            .WithMessage(CafeOrderErrors.InvalidDateRange.Description)
            .OverridePropertyName(nameof(ListMemberCafeOrdersQuery.To));
    }
}
