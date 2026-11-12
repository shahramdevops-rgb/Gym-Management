using Gym.Domain.Shop;

namespace Gym.Domain.Tests.Shop;

/// <summary>The online shop's products, variants and photos (BUSINESS_RULES.md §16 <i>Products</i>).</summary>
public sealed class ShopProductTests
{
    private static readonly Guid Category = Guid.CreateVersion7();

    private static ShopVariantInput Variant(string label, decimal price = 1_200_000m, int stock = 5, Guid? id = null) =>
        new(id, label, price, stock);

    private static ShopProduct Product(params ShopVariantInput[] variants) =>
        ShopProduct.Create("پروتئین وی", "  ۲۴ گرم پروتئین  ", Category, isVisible: true,
            variants.Length == 0 ? [Variant("")] : variants).Value;

    [Fact]
    public void Create_WithOneUnlabelledVariant_KeepsItCleaned()
    {
        var result = ShopProduct.Create("  کراتین  ", "   ", Category, isVisible: false, [Variant("  ", 900_000m, 3)]);

        result.IsSuccess.ShouldBeTrue();
        var product = result.Value;
        product.Name.ShouldBe("کراتین");
        product.Description.ShouldBeNull();
        product.IsVisible.ShouldBeFalse();
        var variant = product.Variants.ShouldHaveSingleItem();
        variant.Label.ShouldBe("");
        variant.Price.ShouldBe(900_000m);
        variant.Stock.ShouldBe(3);
        variant.Position.ShouldBe(0);
        variant.ProductId.ShouldBe(product.Id);
    }

    [Fact]
    public void Create_DescriptionWithSpaces_IsTrimmed()
    {
        Product().Description.ShouldBe("۲۴ گرم پروتئین");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_ReturnsNameRequired(string name)
    {
        ShopProduct.Create(name, null, Category, true, [Variant("")]).Error.ShouldBe(ShopProductErrors.NameRequired);
    }

    [Fact]
    public void Create_NameTooLong_ReturnsNameTooLong()
    {
        var name = new string('ا', ShopProduct.NameMaxLength + 1);

        ShopProduct.Create(name, null, Category, true, [Variant("")]).Error.ShouldBe(ShopProductErrors.NameTooLong);
    }

    [Fact]
    public void Create_DescriptionTooLong_ReturnsDescriptionTooLong()
    {
        var description = new string('ا', ShopProduct.DescriptionMaxLength + 1);

        ShopProduct.Create("کراتین", description, Category, true, [Variant("")])
            .Error.ShouldBe(ShopProductErrors.DescriptionTooLong);
    }

    [Fact]
    public void Create_NoCategory_ReturnsCategoryRequired()
    {
        ShopProduct.Create("کراتین", null, Guid.Empty, true, [Variant("")]).Error.ShouldBe(ShopProductErrors.CategoryRequired);
    }

    [Fact]
    public void Create_NoVariant_ReturnsVariantsRequired()
    {
        ShopProduct.Create("کراتین", null, Category, true, []).Error.ShouldBe(ShopProductErrors.VariantsRequired);
    }

    [Fact]
    public void Create_TooManyVariants_ReturnsTooManyVariants()
    {
        var variants = Enumerable.Range(1, ShopProduct.MaxVariants + 1).Select(i => Variant($"مدل {i}")).ToList();

        ShopProduct.Create("کراتین", null, Category, true, variants).Error.ShouldBe(ShopProductErrors.TooManyVariants);
    }

    [Fact]
    public void Create_TwoVariantsOneUnlabelled_ReturnsVariantLabelRequired()
    {
        ShopProduct.Create("کراتین", null, Category, true, [Variant("شکلاتی"), Variant(" ")])
            .Error.ShouldBe(ShopProductErrors.VariantLabelRequired);
    }

    [Fact]
    public void Create_TwoLabelsSameOnceNormalized_ReturnsVariantLabelDuplicate()
    {
        // An Arabic ye and a Persian one, and a difference in case, are the same label.
        ShopProduct.Create("کراتین", null, Category, true, [Variant("Vanilla شيري"), Variant(" vanilla شیری ")])
            .Error.ShouldBe(ShopProductErrors.VariantLabelDuplicate);
    }

    [Fact]
    public void Create_LabelTooLong_ReturnsVariantLabelTooLong()
    {
        ShopProduct.Create("کراتین", null, Category, true, [Variant(new string('ا', ShopVariant.LabelMaxLength + 1))])
            .Error.ShouldBe(ShopProductErrors.VariantLabelTooLong);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_PriceNotAboveZero_ReturnsVariantPriceNotPositive(decimal price)
    {
        ShopProduct.Create("کراتین", null, Category, true, [Variant("", price)])
            .Error.ShouldBe(ShopProductErrors.VariantPriceNotPositive);
    }

    [Fact]
    public void Create_PriceWithThreeDecimals_ReturnsVariantPriceTooManyDecimals()
    {
        ShopProduct.Create("کراتین", null, Category, true, [Variant("", 10.005m)])
            .Error.ShouldBe(ShopProductErrors.VariantPriceTooManyDecimals);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(ShopVariant.MaxStock + 1)]
    public void Create_StockOutOfRange_ReturnsVariantStockOutOfRange(int stock)
    {
        ShopProduct.Create("کراتین", null, Category, true, [Variant("", stock: stock)])
            .Error.ShouldBe(ShopProductErrors.VariantStockOutOfRange);
    }

    [Fact]
    public void Create_ZeroStock_IsAllowed()
    {
        ShopProduct.Create("کراتین", null, Category, true, [Variant("", stock: 0)]).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Update_KeepsChangesAddsAndRemovesVariantsInTheFormsOrder()
    {
        var product = Product(Variant("شکلاتی"), Variant("وانیلی"), Variant("توت‌فرنگی"));
        var chocolate = product.Variants[0];
        var strawberry = product.Variants[2];

        var result = product.Update("پروتئین وی", null, Category, true,
        [
            Variant("موزی", 1_500_000m, 2),
            Variant("شکلاتی ۲ کیلو", 2_000_000m, 7, chocolate.Id),
            Variant("توت‌فرنگی", 1_200_000m, 0, strawberry.Id),
        ]);

        result.IsSuccess.ShouldBeTrue();
        product.Variants.Select(v => v.Label).ShouldBe(["موزی", "شکلاتی ۲ کیلو", "توت‌فرنگی"]);
        product.Variants.Select(v => v.Position).ShouldBe([0, 1, 2]);
        product.Variants[1].Id.ShouldBe(chocolate.Id);
        product.Variants[1].Price.ShouldBe(2_000_000m);
        product.Variants[1].Stock.ShouldBe(7);
        product.Variants[2].Id.ShouldBe(strawberry.Id);
        product.Variants[2].Stock.ShouldBe(0);
    }

    [Fact]
    public void Update_AVariantIdOfAnotherProduct_ReturnsVariantNotFoundAndChangesNothing()
    {
        var product = Product(Variant("شکلاتی"));

        var result = product.Update("نام تازه", null, Category, false, [Variant("وانیلی", id: Guid.CreateVersion7())]);

        result.Error.ShouldBe(ShopProductErrors.VariantNotFound);
        product.Name.ShouldBe("پروتئین وی");
        product.IsVisible.ShouldBeTrue();
        product.Variants.ShouldHaveSingleItem().Label.ShouldBe("شکلاتی");
    }

    [Fact]
    public void Update_OneVariantListedTwice_ReturnsVariantNotFound()
    {
        var product = Product(Variant("شکلاتی"));
        var id = product.Variants[0].Id;

        product.Update("پروتئین وی", null, Category, true, [Variant("الف", id: id), Variant("ب", id: id)])
            .Error.ShouldBe(ShopProductErrors.VariantNotFound);
    }

    [Fact]
    public void Update_Hide_KeepsTheProduct()
    {
        var product = Product();

        product.Update("پروتئین وی", null, Category, isVisible: false, [Variant("", id: product.Variants[0].Id)]);

        product.IsVisible.ShouldBeFalse();
    }

    [Fact]
    public void AddPhoto_UpToTheLimit_NumbersThemInOrder()
    {
        var product = Product();

        var photos = Enumerable.Range(0, ShopProduct.MaxPhotos).Select(_ => product.AddPhoto(1600, 1200).Value).ToList();

        product.Photos.Select(p => p.Position).ShouldBe(Enumerable.Range(0, ShopProduct.MaxPhotos));
        photos[0].ProductId.ShouldBe(product.Id);
        photos[0].Width.ShouldBe(1600);
        photos[0].Height.ShouldBe(1200);
        product.AddPhoto(800, 800).Error.ShouldBe(ShopProductErrors.TooManyPhotos);
        product.Photos.Count.ShouldBe(ShopProduct.MaxPhotos);
    }

    [Fact]
    public void Photo_Keys_AreMadeFromItsId()
    {
        var photo = Product().AddPhoto(10, 10).Value;

        photo.LargeKey.ShouldBe($"shop/{photo.Id:N}-large.webp");
        photo.SmallKey.ShouldBe($"shop/{photo.Id:N}-small.webp");
    }

    [Fact]
    public void RemovePhoto_TheMainOne_MakesTheNextMainAndClosesTheGap()
    {
        var product = Product();
        var first = product.AddPhoto(10, 10).Value;
        var second = product.AddPhoto(10, 10).Value;
        var third = product.AddPhoto(10, 10).Value;

        product.RemovePhoto(first.Id).Value.ShouldBe(first);

        product.Photos.ShouldBe([second, third]);
        product.Photos.Select(p => p.Position).ShouldBe([0, 1]);
    }

    [Fact]
    public void RemovePhoto_UnknownId_ReturnsPhotoNotFound()
    {
        Product().RemovePhoto(Guid.CreateVersion7()).Error.ShouldBe(ShopProductErrors.PhotoNotFound);
    }

    [Fact]
    public void ReorderPhotos_EachOnce_PutsThemInThatOrder()
    {
        var product = Product();
        var a = product.AddPhoto(10, 10).Value;
        var b = product.AddPhoto(10, 10).Value;
        var c = product.AddPhoto(10, 10).Value;

        product.ReorderPhotos([c.Id, a.Id, b.Id]).IsSuccess.ShouldBeTrue();

        product.Photos.ShouldBe([c, a, b]);
        product.Photos.Select(p => p.Position).ShouldBe([0, 1, 2]);
    }

    [Fact]
    public void ReorderPhotos_MissingRepeatedOrUnknown_ReturnsPhotoOrderInvalidAndKeepsTheOrder()
    {
        var product = Product();
        var a = product.AddPhoto(10, 10).Value;
        var b = product.AddPhoto(10, 10).Value;

        product.ReorderPhotos([b.Id]).Error.ShouldBe(ShopProductErrors.PhotoOrderInvalid);
        product.ReorderPhotos([b.Id, b.Id]).Error.ShouldBe(ShopProductErrors.PhotoOrderInvalid);
        product.ReorderPhotos([b.Id, Guid.CreateVersion7()]).Error.ShouldBe(ShopProductErrors.PhotoOrderInvalid);
        product.Photos.ShouldBe([a, b]);
    }
}
