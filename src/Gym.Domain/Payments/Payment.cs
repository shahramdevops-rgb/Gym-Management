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
/// A "void" (BUSINESS_RULES.md §5) is not a separate code path: it is a full-amount
/// <see cref="RegisterRefundForSubscription"/> whose reason explains the mistake.
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
        Guid receivedByUserId, DateTimeOffset paidAt) =>
        Create(subscriptionId, PaymentKind.Payment, amount, method, referenceNumber, receivedByUserId, paidAt, reason: null);

    /// <summary>
    /// A refund, or a "void" when it happens to be the full amount of a mistaken payment
    /// (BUSINESS_RULES.md §5) — the reason is the only thing that tells the two apart, so there is
    /// no separate void factory. Whether it exceeds the subscription's net paid amount is checked
    /// by the caller, which is the only place that knows the running total.
    /// </summary>
    public static Result<Payment> RegisterRefundForSubscription(
        Guid subscriptionId, decimal amount, PaymentMethod method, string? referenceNumber, string reason,
        Guid receivedByUserId, DateTimeOffset paidAt)
    {
        ArgumentNullException.ThrowIfNull(reason);

        var cleanReason = reason.Trim();
        if (cleanReason.Length == 0)
        {
            return Result.Failure<Payment>(PaymentErrors.RefundReasonRequired);
        }

        if (cleanReason.Length > ReasonMaxLength)
        {
            return Result.Failure<Payment>(PaymentErrors.RefundReasonTooLong);
        }

        return Create(subscriptionId, PaymentKind.Refund, amount, method, referenceNumber, receivedByUserId, paidAt, cleanReason);
    }

    private static Result<Payment> Create(
        Guid subscriptionId, PaymentKind kind, decimal amount, PaymentMethod method, string? referenceNumber,
        Guid receivedByUserId, DateTimeOffset paidAt, string? reason)
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
            Kind = kind,
            Amount = amount,
            Method = method,
            ReferenceNumber = cleanReference,
            PaidAt = paidAt,
            ReceivedByUserId = receivedByUserId,
            Reason = reason,
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
