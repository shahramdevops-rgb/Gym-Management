using FluentValidation;

using Gym.Application.Common.Paging;

namespace Gym.Application.Payments.ListMemberPayments;

public sealed class ListMemberPaymentsValidator : AbstractValidator<ListMemberPaymentsQuery>
{
    public ListMemberPaymentsValidator()
    {
        RuleFor(query => query.Page).ValidPage();
        RuleFor(query => query.PageSize).ValidPageSize();
    }
}
