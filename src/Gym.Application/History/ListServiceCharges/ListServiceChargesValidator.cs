using FluentValidation;

using Gym.Application.Common.Paging;
using Gym.Domain.ServiceCharges;

namespace Gym.Application.History.ListServiceCharges;

public sealed class ListServiceChargesValidator : AbstractValidator<ListServiceChargesQuery>
{
    public ListServiceChargesValidator()
    {
        RuleFor(query => query.Page).ValidPage();
        RuleFor(query => query.PageSize).ValidPageSize();

        // Compared with a business date directly, so no day is added and DateOnly.MaxValue is fine.
        RuleFor(query => query)
            .Must(query => query.From is null || query.To is null || query.From <= query.To)
            .WithErrorCode(ServiceChargeErrors.InvalidDateRange.Code)
            .WithMessage(ServiceChargeErrors.InvalidDateRange.Description)
            .OverridePropertyName(nameof(ListServiceChargesQuery.To));
    }
}
