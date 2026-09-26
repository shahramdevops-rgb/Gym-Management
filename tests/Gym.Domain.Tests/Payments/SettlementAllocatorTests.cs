using Gym.Domain.Payments;

namespace Gym.Domain.Tests.Payments;

/// <summary>BUSINESS_RULES.md §5 <i>Settling several items at once</i>.</summary>
public sealed class SettlementAllocatorTests
{
    private static readonly DateOnly Today = new(2026, 9, 26);

    private static readonly SettlementItem Visit = new(PaymentTargetKind.Subscription, Guid.CreateVersion7(), Today, 150_000m);
    private static readonly SettlementItem Cardio = new(PaymentTargetKind.ServiceCharge, Guid.CreateVersion7(), Today, 40_000m);
    private static readonly SettlementItem Drink = new(PaymentTargetKind.CafeOrder, Guid.CreateVersion7(), Today, 30_000m);

    [Fact]
    public void Allocate_TheWholeTotal_PaysEveryItemInFull()
    {
        var shares = SettlementAllocator.Allocate([Visit, Cardio, Drink], 220_000m).Value;

        shares.Count.ShouldBe(3);
        shares.Single(share => share.Item == Visit).Amount.ShouldBe(150_000m);
        shares.Single(share => share.Item == Cardio).Amount.ShouldBe(40_000m);
        shares.Single(share => share.Item == Drink).Amount.ShouldBe(30_000m);
    }

    [Fact]
    public void Allocate_ItemsInAnyOrder_PaysCafeThenServiceChargeThenSubscription()
    {
        var shares = SettlementAllocator.Allocate([Visit, Cardio, Drink], 220_000m).Value;

        shares.Select(share => share.Item.Kind).ShouldBe(
            [PaymentTargetKind.CafeOrder, PaymentTargetKind.ServiceCharge, PaymentTargetKind.Subscription]);
    }

    [Fact]
    public void Allocate_LessThanTheTotal_LeavesTheSubscriptionPartPaid()
    {
        var shares = SettlementAllocator.Allocate([Visit, Cardio, Drink], 100_000m).Value;

        shares.Single(share => share.Item == Drink).Amount.ShouldBe(30_000m);
        shares.Single(share => share.Item == Cardio).Amount.ShouldBe(40_000m);
        shares.Single(share => share.Item == Visit).Amount.ShouldBe(30_000m);
    }

    [Fact]
    public void Allocate_OnlyEnoughForTheCafe_GivesTheOthersNothing()
    {
        var shares = SettlementAllocator.Allocate([Visit, Cardio, Drink], 20_000m).Value;

        shares.ShouldHaveSingleItem().ShouldBe(new SettlementShare(Drink, 20_000m));
    }

    [Fact]
    public void Allocate_TwoItemsOfOneKind_PaysTheOlderFirst()
    {
        var older = new SettlementItem(PaymentTargetKind.CafeOrder, Guid.CreateVersion7(), Today.AddDays(-3), 50_000m);
        var newer = new SettlementItem(PaymentTargetKind.CafeOrder, Guid.CreateVersion7(), Today, 50_000m);

        var shares = SettlementAllocator.Allocate([newer, older], 60_000m).Value;

        shares.ShouldBe([new SettlementShare(older, 50_000m), new SettlementShare(newer, 10_000m)]);
    }

    [Fact]
    public void Allocate_MoreThanTheTotal_FailsWithOverpayment()
    {
        var result = SettlementAllocator.Allocate([Visit, Cardio, Drink], 220_000.01m);

        result.Error.ShouldBe(PaymentErrors.Overpayment);
    }

    [Fact]
    public void Allocate_NoItems_FailsWithNoItems()
    {
        var result = SettlementAllocator.Allocate([], 10_000m);

        result.Error.ShouldBe(SettlementErrors.NoItems);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Allocate_AmountNotPositive_FailsWithAmountNotPositive(decimal amount)
    {
        var result = SettlementAllocator.Allocate([Visit], amount);

        result.Error.ShouldBe(PaymentErrors.AmountNotPositive);
    }

    [Fact]
    public void Allocate_AmountWithThreeDecimals_FailsWithTooManyDecimals()
    {
        var result = SettlementAllocator.Allocate([Visit], 1.005m);

        result.Error.ShouldBe(PaymentErrors.AmountTooManyDecimals);
    }

    [Fact]
    public void Allocate_ItemThatOwesNothing_Throws()
    {
        var paid = Visit with { Outstanding = 0m };

        Should.Throw<ArgumentException>(() => SettlementAllocator.Allocate([paid], 1m));
    }
}
