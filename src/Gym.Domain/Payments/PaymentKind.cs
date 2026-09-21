namespace Gym.Domain.Payments;

/// <summary>BUSINESS_RULES.md §5. A refund is created only through the refund use case (task 4.5).</summary>
public enum PaymentKind
{
    Payment,
    Refund,
}
