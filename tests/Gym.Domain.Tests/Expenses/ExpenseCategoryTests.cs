using Gym.Domain.Expenses;

namespace Gym.Domain.Tests.Expenses;

public sealed class ExpenseCategoryTests
{
    private const char ArabicYe = (char)0x064A;

    [Fact]
    public void Create_ValidName_TrimsIt()
    {
        var category = ExpenseCategory.Create("  اجاره ").Value;

        category.Name.ShouldBe("اجاره");
    }

    [Fact]
    public void Create_ArabicYeInName_NormalizedNameUsesPersianYe()
    {
        var category = ExpenseCategory.Create($"سا{ArabicYe}ر").Value;

        category.NormalizedName.ShouldBe("سایر");
    }

    [Fact]
    public void Normalize_SameNameAsCreate_GivesTheSameNormalizedName()
    {
        // The seed data spells the column out through Normalize, so it must agree with what a
        // category created through the API stores, or a seeded name would not count as taken.
        ExpenseCategory.Normalize($" سا{ArabicYe}ر ").ShouldBe(ExpenseCategory.Create("سایر").Value.NormalizedName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_FailsWithNameRequired(string name)
    {
        ExpenseCategory.Create(name).Error.ShouldBe(ExpenseCategoryErrors.NameRequired);
    }

    [Fact]
    public void Create_NameOverTheLimit_FailsWithNameTooLong()
    {
        ExpenseCategory.Create(new string('ا', ExpenseCategory.NameMaxLength + 1))
            .Error.ShouldBe(ExpenseCategoryErrors.NameTooLong);
    }

    [Fact]
    public void Rename_ValidName_ReplacesBothNames()
    {
        var category = ExpenseCategory.Create("اجاره").Value;

        category.Rename("اجاره سالن").IsSuccess.ShouldBeTrue();

        category.Name.ShouldBe("اجاره سالن");
        category.NormalizedName.ShouldBe("اجاره سالن");
    }

    [Fact]
    public void Rename_BlankName_FailsAndKeepsTheOldName()
    {
        var category = ExpenseCategory.Create("اجاره").Value;

        category.Rename(" ").Error.ShouldBe(ExpenseCategoryErrors.NameRequired);

        category.Name.ShouldBe("اجاره");
    }
}
