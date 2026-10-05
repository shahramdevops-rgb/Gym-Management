namespace Gym.Application.Payables.CancelPayable;

/// <param name="Reason">
/// Why it is taken out of the register's pending list. Required: the record is kept, and the reason
/// is the whole record of what happened (BUSINESS_RULES.md §9 <i>Cheques and instalments</i>).
/// </param>
public sealed record CancelPayableCommand(string Reason);
