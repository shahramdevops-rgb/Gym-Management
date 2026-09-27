using Gym.Domain.Pricing;

namespace Gym.Domain.Tests.Pricing;

/// <summary>BUSINESS_RULES.md §3 <i>Prices</i>.</summary>
public sealed class PriceListTests
{
    [Fact]
    public void NewPriceList_BeforeTheOwnerSetsIt_HasNoPrices()
    {
        var prices = Empty();

        prices.SessionPrice.ShouldBeNull();
        prices.SingleVisitPrice.ShouldBeNull();
    }

    [Fact]
    public void Update_ValidPrices_SetsBoth()
    {
        var prices = Empty();

        prices.Update(100_000m, 150_000m).IsSuccess.ShouldBeTrue();

        prices.SessionPrice.ShouldBe(100_000m);
        prices.SingleVisitPrice.ShouldBe(150_000m);
    }

    [Fact]
    public void Update_ZeroPrices_Succeeds() =>
        Empty().Update(0m, 0m).IsSuccess.ShouldBeTrue();

    [Theory]
    [InlineData(-1, 150_000)]
    [InlineData(100_000, -1)]
    public void Update_NegativePrice_FailsWithPriceNegativeAndChangesNothing(int sessionPrice, int singleVisitPrice)
    {
        var prices = Empty();
        prices.Update(100_000m, 150_000m);

        prices.Update(sessionPrice, singleVisitPrice).Error.ShouldBe(PricingErrors.PriceNegative);

        prices.SessionPrice.ShouldBe(100_000m);
        prices.SingleVisitPrice.ShouldBe(150_000m);
    }

    [Fact]
    public void Update_ThreeDecimals_FailsWithPriceTooManyDecimals() =>
        Empty().Update(100_000.005m, 150_000m).Error.ShouldBe(PricingErrors.PriceTooManyDecimals);

    [Fact]
    public void Update_BeyondTheMoneyColumn_FailsWithPriceTooLarge() =>
        Empty().Update(100_000m, PriceList.MaxPrice + 1).Error.ShouldBe(PricingErrors.PriceTooLarge);

    [Fact]
    public void Update_AtTheMoneyColumnLimit_Succeeds() =>
        Empty().Update(PriceList.MaxPrice, PriceList.MaxPrice).IsSuccess.ShouldBeTrue();

    /// <summary>
    /// The one row the migration seeds. There is no public constructor, on purpose: nothing in the
    /// app ever creates a price list, so the test builds one the way EF Core does.
    /// </summary>
    private static PriceList Empty() => (PriceList)Activator.CreateInstance(typeof(PriceList), nonPublic: true)!;
}
