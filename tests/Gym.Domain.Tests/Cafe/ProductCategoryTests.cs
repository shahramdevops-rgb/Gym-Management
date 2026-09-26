using Gym.Domain.Cafe;

namespace Gym.Domain.Tests.Cafe;

public sealed class ProductCategoryTests
{
    private const char ArabicKaf = (char)0x0643;

    [Fact]
    public void Create_ValidName_TrimsIt()
    {
        var category = ProductCategory.Create("  نوشیدنی ").Value;

        category.Name.ShouldBe("نوشیدنی");
    }

    [Fact]
    public void Create_ArabicKafInName_NormalizedNameUsesPersianKaf()
    {
        var category = ProductCategory.Create($"{ArabicKaf}افه").Value;

        category.NormalizedName.ShouldBe("کافه");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_FailsWithNameRequired(string name)
    {
        ProductCategory.Create(name).Error.ShouldBe(ProductCategoryErrors.NameRequired);
    }

    [Fact]
    public void Create_NameOverTheLimit_FailsWithNameTooLong()
    {
        ProductCategory.Create(new string('ا', ProductCategory.NameMaxLength + 1))
            .Error.ShouldBe(ProductCategoryErrors.NameTooLong);
    }

    [Fact]
    public void Rename_ValidName_ReplacesBothForms()
    {
        var category = ProductCategory.Create("نوشیدنی").Value;

        var result = category.Rename("مکمل");

        result.IsSuccess.ShouldBeTrue();
        category.Name.ShouldBe("مکمل");
        category.NormalizedName.ShouldBe("مکمل");
    }

    [Fact]
    public void Create_ValidName_IsActive()
    {
        ProductCategory.Create("نوشیدنی").Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Deactivate_ActiveCategory_SwitchesTheShelfOff()
    {
        var category = ProductCategory.Create("نوشیدنی").Value;

        category.Deactivate();

        category.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Deactivate_AlreadyInactiveCategory_ChangesNothing()
    {
        var category = ProductCategory.Create("نوشیدنی").Value;
        category.Deactivate();

        category.Deactivate();

        category.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Activate_InactiveCategory_SwitchesItBackOn()
    {
        var category = ProductCategory.Create("نوشیدنی").Value;
        category.Deactivate();

        category.Activate();

        category.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Rename_InactiveCategory_IsAllowedAndKeepsItOff()
    {
        var category = ProductCategory.Create("نوشیدنی").Value;
        category.Deactivate();

        category.Rename("مکمل").IsSuccess.ShouldBeTrue();

        category.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Rename_BlankName_ChangesNothing()
    {
        var category = ProductCategory.Create("نوشیدنی").Value;

        category.Rename("  ").Error.ShouldBe(ProductCategoryErrors.NameRequired);

        category.Name.ShouldBe("نوشیدنی");
    }
}
