using FluentValidation;

using Gym.Application.Common.Paging;

namespace Gym.Application.History.ListPayments;

/// <summary>
/// The shape of the request only. Whether Staff may reach that far back depends on who is asking
/// and on today, so the handler checks it (<see cref="PaymentHistoryWindow"/>).
/// </summary>
public sealed class ListPaymentsValidator : AbstractValidator<ListPaymentsQuery>
{
    public ListPaymentsValidator()
    {
        RuleFor(query => query.Page).ValidPage();
        RuleFor(query => query.PageSize).ValidPageSize();

        Include(new PaymentsFilterValidator());
    }
}
