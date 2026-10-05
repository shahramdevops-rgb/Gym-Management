namespace Gym.Application.Cheques.RegisterCheque;

/// <param name="DueDate">The date written on the cheque. Any date: an old cheque can be entered late.</param>
/// <param name="Payee">Who the cheque is made out to (در وجه).</param>
public sealed record RegisterChequeCommand(
    decimal Amount,
    DateOnly DueDate,
    string Payee,
    string Description);
