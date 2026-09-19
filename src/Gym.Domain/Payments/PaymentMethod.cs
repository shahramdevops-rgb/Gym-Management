namespace Gym.Domain.Payments;

/// <summary>How a payment was received (BUSINESS_RULES.md §0, decided with the developer in task 4.4).</summary>
public enum PaymentMethod
{
    Cash,
    Card,
    BankTransfer,
}
