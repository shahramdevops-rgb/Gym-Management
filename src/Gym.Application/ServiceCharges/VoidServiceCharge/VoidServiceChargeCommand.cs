namespace Gym.Application.ServiceCharges.VoidServiceCharge;

/// <param name="Reason">
/// Why the charge is being undone. Required: a voided charge is kept, not erased, and the reason
/// is the whole record of what happened (BUSINESS_RULES.md §5, §7).
/// </param>
public sealed record VoidServiceChargeCommand(string Reason);
