using FluentValidation;

using Gym.Application.Common.Paging;

namespace Gym.Application.Cheques.ListCheques;

public sealed class ListChequesValidator : AbstractValidator<ListChequesQuery>
{
    public ListChequesValidator()
    {
        RuleFor(query => query.Page).ValidPage();
        RuleFor(query => query.PageSize).ValidPageSize();
    }
}
