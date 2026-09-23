namespace Gym.Domain.Payments;

/// <summary>
/// Which of the things a payment can belong to (BUSINESS_RULES.md §5) an item is. Shared by the
/// member's debt breakdown and their payment history, so both lists label a row the same way.
/// Phase 7 adds <c>CafeOrder</c>.
/// </summary>
public enum PaymentTargetKind
{
    Subscription,
    ServiceCharge,
}
