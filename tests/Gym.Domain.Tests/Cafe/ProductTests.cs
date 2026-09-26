using Gym.Domain.Cafe;

namespace Gym.Domain.Tests.Cafe;

public sealed class ProductTests
{
    private const char ArabicYe = (char)0x064A;

    private static readonly Guid CategoryId = Guid.CreateVersion7();

    [Fact]
    public void Create_ValidProduct_IsActiveWithTheGivenValues()
    {
        var product = Product.Create("  آب معدنی ", CategoryId, 15_000m).Value;

        product.IsActive.ShouldBeTrue();
        product.Name.ShouldBe("آب معدنی");
        product.CategoryId.ShouldBe(CategoryId);
        product.Price.ShouldBe(15_000m);
    }

    [Fact]
    public void Create_ArabicYeInName_NormalizedNameUsesPersianYe()
    {
        var product = Product.Create($"نوش{ArabicYe}دنی", CategoryId, 0m).Value;

        product.NormalizedName.ShouldBe("نوشیدنی");
    }

    [Fact]
    public void Create_ZeroPrice_IsAllowed()
    {
        // A giveaway is priced at zero rather than left out of the price list; §5 already says
        // a free item owes nothing.
        Product.Create("لیوان آب", CategoryId, 0m).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_FailsWithNameRequired(string name)
    {
        Product.Create(name, CategoryId, 15_000m).Error.ShouldBe(ProductErrors.NameRequired);
    }

    [Fact]
    public void Create_NameOverTheLimit_FailsWithNameTooLong()
    {
        Product.Create(new string('ا', Product.NameMaxLength + 1), CategoryId, 15_000m)
            .Error.ShouldBe(ProductErrors.NameTooLong);
    }

    [Fact]
    public void Create_EmptyCategoryId_FailsWithCategoryRequired()
    {
        Product.Create("آب معدنی", Guid.Empty, 15_000m).Error.ShouldBe(ProductErrors.CategoryRequired);
    }

    [Fact]
    public void Create_NegativePrice_FailsWithPriceNegative()
    {
        Product.Create("آب معدنی", CategoryId, -1m).Error.ShouldBe(ProductErrors.PriceNegative);
    }

    [Fact]
    public void Create_PriceOverTheColumnLimit_FailsWithPriceTooLarge()
    {
        Product.Create("آب معدنی", CategoryId, Product.MaxPrice + 1m).Error.ShouldBe(ProductErrors.PriceTooLarge);
    }

    [Fact]
    public void Create_PriceWithThreeDecimals_FailsInsteadOfRounding()
    {
        // Refused, never rounded: rounding money silently is how a price nobody typed appears.
        Product.Create("آب معدنی", CategoryId, 15_000.005m).Error.ShouldBe(ProductErrors.PriceTooManyDecimals);
    }

    [Fact]
    public void Update_NewNameCategoryAndPrice_ReplacesThemAll()
    {
        var product = Product.Create("آب معدنی", CategoryId, 15_000m).Value;
        var otherCategory = Guid.CreateVersion7();

        var result = product.Update("آب معدنی بزرگ", otherCategory, 20_000m);

        result.IsSuccess.ShouldBeTrue();
        product.Name.ShouldBe("آب معدنی بزرگ");
        product.CategoryId.ShouldBe(otherCategory);
        product.Price.ShouldBe(20_000m);
    }

    [Fact]
    public void Update_InvalidPrice_ChangesNothing()
    {
        var product = Product.Create("آب معدنی", CategoryId, 15_000m).Value;

        product.Update("آب معدنی بزرگ", CategoryId, -1m).Error.ShouldBe(ProductErrors.PriceNegative);

        product.Name.ShouldBe("آب معدنی");
        product.Price.ShouldBe(15_000m);
    }

    [Fact]
    public void Update_InactiveProduct_IsAllowed()
    {
        // A discontinued item can be corrected before it is offered again.
        var product = Product.Create("آب معدنی", CategoryId, 15_000m).Value;
        product.Deactivate();

        product.Update("آب معدنی بزرگ", CategoryId, 20_000m).IsSuccess.ShouldBeTrue();
        product.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Deactivate_ActiveProduct_MakesItUnsellable()
    {
        var product = Product.Create("آب معدنی", CategoryId, 15_000m).Value;

        product.Deactivate();

        product.IsActive.ShouldBeFalse();
        product.EnsureCanBeSold().Error.ShouldBe(ProductErrors.Inactive);
    }

    [Fact]
    public void Deactivate_AlreadyInactiveProduct_ChangesNothing()
    {
        var product = Product.Create("آب معدنی", CategoryId, 15_000m).Value;
        product.Deactivate();

        product.Deactivate();

        product.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Activate_InactiveProduct_MakesItSellableAgain()
    {
        var product = Product.Create("آب معدنی", CategoryId, 15_000m).Value;
        product.Deactivate();

        product.Activate();

        product.IsActive.ShouldBeTrue();
        product.EnsureCanBeSold().IsSuccess.ShouldBeTrue();
    }
}
