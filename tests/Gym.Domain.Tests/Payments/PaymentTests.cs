using Gym.Domain.Payments;

namespace Gym.Domain.Tests.Payments;

public sealed class PaymentTests
{
    private static readonly Guid SubscriptionId = Guid.CreateVersion7();
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly DateTimeOffset PaidAt = DateTimeOffset.UtcNow;

    [Fact]
    public void RegisterForSubscription_PositiveAmount_SucceedsAsAPayment()
    {
        var registered = Payment.RegisterForSubscription(SubscriptionId, 500_000m, PaymentMethod.Cash, null, UserId, PaidAt);

        registered.IsSuccess.ShouldBeTrue();
        var payment = registered.Value;
        payment.SubscriptionId.ShouldBe(SubscriptionId);
        payment.CafeOrderId.ShouldBeNull();
        payment.Kind.ShouldBe(PaymentKind.Payment);
        payment.Amount.ShouldBe(500_000m);
        payment.Method.ShouldBe(PaymentMethod.Cash);
        payment.ReceivedByUserId.ShouldBe(UserId);
        payment.PaidAt.ShouldBe(PaidAt);
        payment.Reason.ShouldBeNull();
    }

    [Fact]
    public void RegisterForSubscription_BlankReferenceNumber_IsStoredAsNull()
    {
        var payment = Payment.RegisterForSubscription(SubscriptionId, 100m, PaymentMethod.Card, "   ", UserId, PaidAt).Value;

        payment.ReferenceNumber.ShouldBeNull();
    }

    [Fact]
    public void RegisterForSubscription_ReferenceNumberWithSurroundingSpaces_IsTrimmed()
    {
        var payment = Payment.RegisterForSubscription(SubscriptionId, 100m, PaymentMethod.Card, "  ABC123  ", UserId, PaidAt).Value;

        payment.ReferenceNumber.ShouldBe("ABC123");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RegisterForSubscription_ZeroOrNegativeAmount_FailsWithAmountNotPositive(decimal amount)
    {
        Payment.RegisterForSubscription(SubscriptionId, amount, PaymentMethod.Cash, null, UserId, PaidAt)
            .Error.ShouldBe(PaymentErrors.AmountNotPositive);
    }

    [Fact]
    public void RegisterForSubscription_AmountAboveTheColumnLimit_FailsWithAmountTooLarge()
    {
        Payment.RegisterForSubscription(SubscriptionId, Payment.MaxAmount + 0.01m, PaymentMethod.Cash, null, UserId, PaidAt)
            .Error.ShouldBe(PaymentErrors.AmountTooLarge);
    }

    [Fact]
    public void RegisterForSubscription_AmountWithThreeDecimals_FailsInsteadOfRounding()
    {
        Payment.RegisterForSubscription(SubscriptionId, 100.005m, PaymentMethod.Cash, null, UserId, PaidAt)
            .Error.ShouldBe(PaymentErrors.AmountTooManyDecimals);
    }

    [Fact]
    public void RegisterForSubscription_ReferenceNumberOver100Characters_FailsWithReferenceNumberTooLong()
    {
        var referenceNumber = new string('1', Payment.ReferenceNumberMaxLength + 1);

        Payment.RegisterForSubscription(SubscriptionId, 100m, PaymentMethod.Card, referenceNumber, UserId, PaidAt)
            .Error.ShouldBe(PaymentErrors.ReferenceNumberTooLong);
    }
}

public sealed class PaymentStatusCalculatorTests
{
    [Fact]
    public void Calculate_NetPaidZero_ReturnsUnpaid()
    {
        PaymentStatusCalculator.Calculate(900_000m, 0m).ShouldBe(PaymentStatus.Unpaid);
    }

    [Fact]
    public void Calculate_NetPaidBetweenZeroAndPrice_ReturnsPartial()
    {
        PaymentStatusCalculator.Calculate(900_000m, 400_000m).ShouldBe(PaymentStatus.Partial);
    }

    [Fact]
    public void Calculate_NetPaidEqualToPrice_ReturnsPaid()
    {
        PaymentStatusCalculator.Calculate(900_000m, 900_000m).ShouldBe(PaymentStatus.Paid);
    }

    [Fact]
    public void Calculate_NetPaidAbovePrice_ReturnsPaid()
    {
        PaymentStatusCalculator.Calculate(900_000m, 950_000m).ShouldBe(PaymentStatus.Paid);
    }

    [Fact]
    public void Calculate_ZeroPricePlanWithNoPayments_ReturnsPaid()
    {
        // A free subscription owes nothing, so it starts out Paid rather than Unpaid.
        PaymentStatusCalculator.Calculate(0m, 0m).ShouldBe(PaymentStatus.Paid);
    }
}
