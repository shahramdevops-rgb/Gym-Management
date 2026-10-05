namespace Gym.Application.Payables.RevertPayable;

/// <param name="Reason">
/// Why the payment was marked by mistake. Required: its expense is voided with this same reason
/// (BUSINESS_RULES.md §9 <i>Cheques and instalments</i>).
/// </param>
public sealed record RevertPayableCommand(string Reason);
