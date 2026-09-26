namespace Gym.Application.Cafe.CancelCafeOrder;

/// <param name="Reason">
/// Why the sale is being undone. Required: a cancelled order is kept, not erased, and the reason
/// is the whole record of what happened (BUSINESS_RULES.md §8).
/// </param>
public sealed record CancelCafeOrderCommand(string Reason);
