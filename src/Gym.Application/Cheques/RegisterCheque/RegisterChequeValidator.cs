using FluentValidation;

namespace Gym.Application.Cheques.RegisterCheque;

public sealed class RegisterChequeValidator : AbstractValidator<RegisterChequeCommand>
{
    public RegisterChequeValidator()
    {
        RuleFor(command => command.Amount).ValidAmount();
        RuleFor(command => command.Payee).ValidPayee();
        RuleFor(command => command.Description).ValidDescription();
    }
}
