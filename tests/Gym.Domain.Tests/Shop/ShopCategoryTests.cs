using Gym.Domain.Shop;

namespace Gym.Domain.Tests.Shop;

/// <summary>The online shop's categories (BUSINESS_RULES.md §16 <i>Products</i>).</summary>
public sealed class ShopCategoryTests
{
    [Fact]
    public void Create_NameWithSpaces_IsTrimmedAndNormalized()
    {
        var category = ShopCategory.Create("  مکمل‌ها ي  ").Value;

        category.Name.ShouldBe("مکمل‌ها ي");
        category.NormalizedName.ShouldBe(ShopCategory.Create("مکمل‌ها ی").Value.NormalizedName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_ReturnsNameRequired(string name)
    {
        ShopCategory.Create(name).Error.ShouldBe(ShopCategoryErrors.NameRequired);
    }

    [Fact]
    public void Rename_TooLong_ReturnsNameTooLongAndKeepsTheOldName()
    {
        var category = ShopCategory.Create("پوشاک").Value;

        category.Rename(new string('ا', ShopCategory.NameMaxLength + 1)).Error.ShouldBe(ShopCategoryErrors.NameTooLong);

        category.Name.ShouldBe("پوشاک");
    }
}
