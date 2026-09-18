using Gym.Domain.Plans;

namespace Gym.Domain.Tests.Plans;

public sealed class PlanTests
{
    private const char ArabicYe = (char)0x064A;

    [Fact]
    public void Create_LimitedPlan_IsActiveWithTheGivenValues()
    {
        var plan = Plan.Create("  یک ماهه ۱۲ جلسه ", 30, 12, 900_000m).Value;

        plan.IsActive.ShouldBeTrue();
        plan.Name.ShouldBe("یک ماهه ۱۲ جلسه");
        plan.DurationDays.ShouldBe(30);
        plan.SessionCount.ShouldBe(12);
        plan.IsUnlimited.ShouldBeFalse();
        plan.Price.ShouldBe(900_000m);
    }

    [Fact]
    public void Create_NullSessionCount_IsUnlimited()
    {
        var plan = Plan.Create("ماهانه آزاد", 30, null, 1_500_000m).Value;

        plan.SessionCount.ShouldBeNull();
        plan.IsUnlimited.ShouldBeTrue();
    }

    [Fact]
    public void Create_ArabicYeInName_NormalizedNameUsesPersianYe()
    {
        var plan = Plan.Create($"و{ArabicYe}ژه", 30, null, 0m).Value;

        plan.NormalizedName.ShouldBe("ویژه");
    }

    [Fact]
    public void Create_ZeroPrice_IsAllowed()
    {
        Plan.Create("جلسه آزمایشی", 1, 1, 0m).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_FailsWithNameRequired(string name)
    {
        Plan.Create(name, 30, null, 0m).Error.ShouldBe(PlanErrors.NameRequired);
    }

    [Fact]
    public void Create_NameOver100Characters_FailsWithNameTooLong()
    {
        Plan.Create(new string('ا', Plan.NameMaxLength + 1), 30, null, 0m).Error.ShouldBe(PlanErrors.NameTooLong);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(366)]
    public void Create_DurationOutOfRange_FailsWithDurationInvalid(int durationDays)
    {
        Plan.Create("پلن", durationDays, null, 0m).Error.ShouldBe(PlanErrors.DurationInvalid);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(365)]
    public void Create_DurationAtTheLimits_Succeeds(int durationDays)
    {
        Plan.Create("پلن", durationDays, null, 0m).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(366)]
    public void Create_SessionCountOutOfRange_FailsWithSessionCountInvalid(int sessionCount)
    {
        Plan.Create("پلن", 30, sessionCount, 0m).Error.ShouldBe(PlanErrors.SessionCountInvalid);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(365)]
    public void Create_SessionCountAtTheLimits_Succeeds(int sessionCount)
    {
        Plan.Create("پلن", 30, sessionCount, 0m).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_NegativePrice_FailsWithPriceNegative()
    {
        Plan.Create("پلن", 30, null, -0.01m).Error.ShouldBe(PlanErrors.PriceNegative);
    }

    [Fact]
    public void Create_PriceAboveTheColumnLimit_FailsWithPriceTooLarge()
    {
        Plan.Create("پلن", 30, null, Plan.MaxPrice + 0.01m).Error.ShouldBe(PlanErrors.PriceTooLarge);
    }

    [Fact]
    public void Create_PriceWithThreeDecimals_FailsInsteadOfRounding()
    {
        Plan.Create("پلن", 30, null, 900_000.005m).Error.ShouldBe(PlanErrors.PriceTooManyDecimals);
    }

    [Fact]
    public void Create_PriceWithTrailingZeroDecimals_IsAllowed()
    {
        // 12.500 is the same amount as 12.50; only digits that would be lost are refused.
        Plan.Create("پلن", 30, null, 12.500m).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Update_ValidValues_ReplacesThemAndKeepsTheActiveFlag()
    {
        var plan = Plan.Create("پلن", 30, 12, 900_000m).Value;
        plan.Deactivate();

        var result = plan.Update("پلن سه ماهه", 90, null, 2_500_000m);

        result.IsSuccess.ShouldBeTrue();
        plan.Name.ShouldBe("پلن سه ماهه");
        plan.DurationDays.ShouldBe(90);
        plan.SessionCount.ShouldBeNull();
        plan.Price.ShouldBe(2_500_000m);
        plan.IsActive.ShouldBeFalse("editing is not reactivating.");
    }

    [Fact]
    public void Update_InvalidValue_ChangesNothing()
    {
        var plan = Plan.Create("پلن", 30, 12, 900_000m).Value;

        plan.Update("نام جدید", 30, 0, 900_000m).IsFailure.ShouldBeTrue();

        plan.Name.ShouldBe("پلن");
        plan.SessionCount.ShouldBe(12);
    }

    [Fact]
    public void EnsureCanBeSold_ActivePlan_Succeeds()
    {
        Plan.Create("پلن", 30, null, 0m).Value.EnsureCanBeSold().IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void EnsureCanBeSold_InactivePlan_FailsWithInactive()
    {
        var plan = Plan.Create("پلن", 30, null, 0m).Value;
        plan.Deactivate();

        plan.EnsureCanBeSold().Error.ShouldBe(PlanErrors.Inactive);
    }

    [Fact]
    public void EnsureCanBeSold_ReactivatedPlan_SucceedsAgain()
    {
        var plan = Plan.Create("پلن", 30, null, 0m).Value;
        plan.Deactivate();
        plan.Activate();

        plan.EnsureCanBeSold().IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Deactivate_Twice_StaysInactive()
    {
        var plan = Plan.Create("پلن", 30, null, 0m).Value;

        plan.Deactivate();
        plan.Deactivate();

        plan.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Activate_AlreadyActive_StaysActive()
    {
        var plan = Plan.Create("پلن", 30, null, 0m).Value;

        plan.Activate();

        plan.IsActive.ShouldBeTrue();
    }
}
