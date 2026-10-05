using FluentValidation;

using Gym.Application.Common.Paging;

namespace Gym.Application.Payables.ListPayables;

public sealed class ListPayablesValidator : AbstractValidator<ListPayablesQuery>
{
    public ListPayablesValidator()
    {
        RuleFor(query => query.Status).IsInEnum();
        RuleFor(query => query.Kind).IsInEnum();
        RuleFor(query => query.Page).ValidPage();
        RuleFor(query => query.PageSize).ValidPageSize();
    }
}
