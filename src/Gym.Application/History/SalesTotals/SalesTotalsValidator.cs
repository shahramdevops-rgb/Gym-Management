using FluentValidation;

using Gym.Application.History.ListSales;

namespace Gym.Application.History.SalesTotals;

public sealed class SalesTotalsValidator : AbstractValidator<SalesTotalsQuery>
{
    public SalesTotalsValidator()
    {
        Include(new SalesFilterValidator());
    }
}
