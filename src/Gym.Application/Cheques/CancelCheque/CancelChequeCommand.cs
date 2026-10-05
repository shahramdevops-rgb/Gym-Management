namespace Gym.Application.Cheques.CancelCheque;

/// <param name="Reason">
/// Why the cheque is taken out of the register's pending list. Required: the cheque is kept, and
/// the reason is the whole record of what happened (BUSINESS_RULES.md §9 <i>Cheques</i>).
/// </param>
public sealed record CancelChequeCommand(string Reason);
