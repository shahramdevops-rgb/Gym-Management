using Gym.Domain.Common;

namespace Gym.Domain.Payments;

/// <summary>
/// Money moving for a subscription or a cafe order (BUSINESS_RULES.md §5). Never edited or
/// deleted: a mistaken entry is fixed with a full refund whose reason explains the mistake.
/// </summary>
/// <remarks>
/// <see cref="CafeOrderId"/> exists because the documented entity has it (a payment belongs to
/// exactly one of a subscription or a cafe order), but nothing sets it before cafe orders exist
/// (Phase 7); the database's one-target check constraint is enforced from this task on regardless.
/// Registering a refund (<see cref="PaymentKind.Refund"/>) is task 4.5's use case; this task only
/// builds <see cref="RegisterForSubscription"/>.
/// </remarks>
public sealed class Payment : Entity
{
    /// <summary>The column is <c>numeric(18,2)</c>, matching every other money column.</summary>
    public const int AmountDecimals = 2;

    public const decimal MaxAmount = 9_999_999_999_999_999.99m;

    public const int ReferenceNumberMaxLength = 100;

    public const int ReasonMaxLength = 500;

    // For EF Core.
    private Payment()
    {
    }

    public Guid? SubscriptionId { get; private set; }

    public Guid? CafeOrderId { get; private set; }

    public PaymentKind Kind { get; private set; }

    public decimal Amount { get; private set; }

    public PaymentMethod Method { get; private set; }

    public string? ReferenceNumber { get; private set; }

    /// <summary>A moment (UTC): when the payment was received, not a business date.</summary>
    public DateTimeOffset PaidAt { get; private set; }

    public Guid ReceivedByUserId { get; private set; }

    /// <summary>Required for a refund; always null for a payment.</summary>
    public string? Reason { get; private set; }

    public static Result<Payment> RegisterForSubscription(
        Guid subscriptionId, decimal amount, PaymentMethod method, string? referenceNumber,
        Guid receivedByUserId, DateTimeOffset paidAt)
    {
        var amountError = CheckAmount(amount);
        if (amountError is not null)
        {
            return Result.Failure<Payment>(amountError);
        }

        var cleanReference = referenceNumber?.Trim();
        if (cleanReference is { Length: 0 })
        {
            cleanReference = null;
        }

        if (cleanReference is not null && cleanReference.Length > ReferenceNumberMaxLength)
        {
            return Result.Failure<Payment>(PaymentErrors.ReferenceNumberTooLong);
        }

        return new Payment
        {
            SubscriptionId = subscriptionId,
            Kind = PaymentKind.Payment,
            Amount = amount,
            Method = method,
            ReferenceNumber = cleanReference,
            PaidAt = paidAt,
            ReceivedByUserId = receivedByUserId,
        };
    }

    /// <summary>
    /// Also used by <c>PaymentRules</c>, so the form hears the same answer as the entity
    /// (the same split <see cref="Domain.Plans.Plan.CheckPrice"/> uses for a price).
    /// </summary>
    public static Error? CheckAmount(decimal amount)
    {
        if (amount <= 0)
        {
            return PaymentErrors.AmountNotPositive;
        }

        if (amount > MaxAmount)
        {
            return PaymentErrors.AmountTooLarge;
        }

        return decimal.Round(amount, AmountDecimals) != amount ? PaymentErrors.AmountTooManyDecimals : null;
    }
}
