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

    // ---- RegisterRefundForSubscription ----

    [Fact]
    public void RegisterRefundForSubscription_PositiveAmountWithReason_SucceedsAsARefund()
    {
        var registered = Payment.RegisterRefundForSubscription(
            SubscriptionId, 300_000m, PaymentMethod.Cash, null, "اشتباه در ثبت مبلغ", UserId, PaidAt);

        registered.IsSuccess.ShouldBeTrue();
        var refund = registered.Value;
        refund.Kind.ShouldBe(PaymentKind.Refund);
        refund.Amount.ShouldBe(300_000m);
        refund.Reason.ShouldBe("اشتباه در ثبت مبلغ");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RegisterRefundForSubscription_BlankReason_FailsWithRefundReasonRequired(string reason)
    {
        Payment.RegisterRefundForSubscription(SubscriptionId, 100m, PaymentMethod.Cash, null, reason, UserId, PaidAt)
            .Error.ShouldBe(PaymentErrors.RefundReasonRequired);
    }

    [Fact]
    public void RegisterRefundForSubscription_ReasonOver500Characters_FailsWithRefundReasonTooLong()
    {
        var reason = new string('د', Payment.ReasonMaxLength + 1);

        Payment.RegisterRefundForSubscription(SubscriptionId, 100m, PaymentMethod.Cash, null, reason, UserId, PaidAt)
            .Error.ShouldBe(PaymentErrors.RefundReasonTooLong);
    }

    [Fact]
    public void RegisterRefundForSubscription_ZeroAmount_FailsWithAmountNotPositive()
    {
        // The amount and reference-number checks are shared with RegisterForSubscription
        // through the entity's private Create; one case here is enough to prove that.
        Payment.RegisterRefundForSubscription(SubscriptionId, 0m, PaymentMethod.Cash, null, "دلیل", UserId, PaidAt)
            .Error.ShouldBe(PaymentErrors.AmountNotPositive);
    }

    [Fact]
    public void RegisterForSubscription_OnItsOwn_IsPartOfNoSettlement()
    {
        Payment.RegisterForSubscription(SubscriptionId, 100m, PaymentMethod.Cash, null, UserId, PaidAt)
            .Value.SettlementId.ShouldBeNull();
    }

    [Fact]
    public void JoinSettlement_APayment_RecordsTheSettlement()
    {
        var payment = Payment.RegisterForSubscription(SubscriptionId, 100m, PaymentMethod.Cash, null, UserId, PaidAt).Value;
        var settlementId = Guid.CreateVersion7();

        payment.JoinSettlement(settlementId);

        payment.SettlementId.ShouldBe(settlementId);
    }

    [Fact]
    public void JoinSettlement_ARefund_Throws()
    {
        var refund = Payment.RegisterRefundForSubscription(
            SubscriptionId, 100m, PaymentMethod.Cash, null, "mistake", UserId, PaidAt).Value;

        Should.Throw<InvalidOperationException>(() => refund.JoinSettlement(Guid.CreateVersion7()));
        refund.SettlementId.ShouldBeNull();
    }

    [Fact]
    public void JoinSettlement_AlreadyInASettlement_Throws()
    {
        var payment = Payment.RegisterForSubscription(SubscriptionId, 100m, PaymentMethod.Cash, null, UserId, PaidAt).Value;
        var first = Guid.CreateVersion7();
        payment.JoinSettlement(first);

        Should.Throw<InvalidOperationException>(() => payment.JoinSettlement(Guid.CreateVersion7()));
        payment.SettlementId.ShouldBe(first);
    }

    [Fact]
    public void JoinSettlement_EmptyId_Throws()
    {
        var payment = Payment.RegisterForSubscription(SubscriptionId, 100m, PaymentMethod.Cash, null, UserId, PaidAt).Value;

        Should.Throw<ArgumentException>(() => payment.JoinSettlement(Guid.Empty));
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
