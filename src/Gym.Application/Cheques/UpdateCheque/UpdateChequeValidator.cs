using FluentValidation;

namespace Gym.Application.Cheques.UpdateCheque;

public sealed class UpdateChequeValidator : AbstractValidator<UpdateChequeCommand>
{
    public UpdateChequeValidator()
    {
        RuleFor(command => command.Amount).ValidAmount();
        RuleFor(command => command.Payee).ValidPayee();
        RuleFor(command => command.Description).ValidDescription();
    }
}
