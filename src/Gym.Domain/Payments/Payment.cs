using Gym.Domain.Common;

namespace Gym.Domain.Payments;

/// <summary>
/// Money moving for a subscription, a service charge or a cafe order (BUSINESS_RULES.md §5).
/// Never edited or deleted: a mistaken entry is fixed with a full refund whose reason explains
/// the mistake.
/// </summary>
/// <remarks>
/// <see cref="CafeOrderId"/> exists because the documented entity has it, but nothing sets it
/// before cafe orders exist (Phase 7); the database's one-target check constraint is enforced
/// from this table's first row regardless. <see cref="ServiceChargeId"/> arrived with the cardio
/// charge (task 5.7) and is set. A "void" (BUSINESS_RULES.md §5) is not a separate code path: it
/// is a full-amount refund whose reason explains the mistake.
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

    /// <summary>BUSINESS_RULES.md §7 <i>Gym services</i>: the third thing a payment can belong to.</summary>
    public Guid? ServiceChargeId { get; private set; }

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
        Create(Target.Subscription(subscriptionId), PaymentKind.Payment, amount, method, referenceNumber, receivedByUserId, paidAt, reason: null);

    /// <summary>
    /// A refund, or a "void" when it happens to be the full amount of a mistaken payment
    /// (BUSINESS_RULES.md §5) — the reason is the only thing that tells the two apart, so there is
    /// no separate void factory. Whether it exceeds the subscription's net paid amount is checked
    /// by the caller, which is the only place that knows the running total.
    /// </summary>
    public static Result<Payment> RegisterRefundForSubscription(
        Guid subscriptionId, decimal amount, PaymentMethod method, string? referenceNumber, string reason,
        Guid receivedByUserId, DateTimeOffset paidAt) =>
        CreateRefund(Target.Subscription(subscriptionId), amount, method, referenceNumber, reason, receivedByUserId, paidAt);

    public static Result<Payment> RegisterForServiceCharge(
        Guid serviceChargeId, decimal amount, PaymentMethod method, string? referenceNumber,
        Guid receivedByUserId, DateTimeOffset paidAt) =>
        Create(Target.ServiceCharge(serviceChargeId), PaymentKind.Payment, amount, method, referenceNumber, receivedByUserId, paidAt, reason: null);

    /// <summary>
    /// The service-charge twin of <see cref="RegisterRefundForSubscription"/>. Voiding a charge
    /// that has been paid writes one of these for the whole net paid amount, so the gym never
    /// holds money for something it has decided is not owed (BUSINESS_RULES.md §5: there is no
    /// wallet and no credit balance).
    /// </summary>
    public static Result<Payment> RegisterRefundForServiceCharge(
        Guid serviceChargeId, decimal amount, PaymentMethod method, string? referenceNumber, string reason,
        Guid receivedByUserId, DateTimeOffset paidAt) =>
        CreateRefund(Target.ServiceCharge(serviceChargeId), amount, method, referenceNumber, reason, receivedByUserId, paidAt);

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

    private static Result<Payment> CreateRefund(
        Target target, decimal amount, PaymentMethod method, string? referenceNumber, string reason,
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

        return Create(target, PaymentKind.Refund, amount, method, referenceNumber, receivedByUserId, paidAt, cleanReason);
    }

    private static Result<Payment> Create(
        Target target, PaymentKind kind, decimal amount, PaymentMethod method, string? referenceNumber,
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
            SubscriptionId = target.SubscriptionId,
            ServiceChargeId = target.ServiceChargeId,
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
    /// The one thing this payment belongs to (BUSINESS_RULES.md §5), so the rule is in the type
    /// system rather than in a comment above a pair of nullable parameters. Phase 7 adds a third
    /// factory here for cafe orders; the check constraint in the database says the same.
    /// </summary>
    private readonly record struct Target(Guid? SubscriptionId, Guid? ServiceChargeId)
    {
        public static Target Subscription(Guid id) => new(id, null);

        public static Target ServiceCharge(Guid id) => new(null, id);
    }
}
