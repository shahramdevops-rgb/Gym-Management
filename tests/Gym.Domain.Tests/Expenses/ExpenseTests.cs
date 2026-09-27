using Gym.Domain.Common;
using Gym.Domain.Expenses;

namespace Gym.Domain.Tests.Expenses;

/// <summary>
/// BUSINESS_RULES.md §9, as far as one row can answer it. Whether the category exists is a
/// cross-table question the handlers answer, so it is covered by the integration tests instead.
/// </summary>
public sealed class ExpenseTests
{
    private static readonly Guid CategoryId = Guid.NewGuid();
    private static readonly Guid OtherCategoryId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 9, 27);
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 10, 0, 0, TimeSpan.Zero);

    // ---- Record ----

    [Fact]
    public void Record_ValidInput_SetsAllFieldsAndIsNotVoided()
    {
        var expense = Record(amount: 5_000_000m, description: "  اجاره مهر ", referenceNumber: " 12345 ").Value;

        expense.Amount.ShouldBe(5_000_000m);
        expense.CategoryId.ShouldBe(CategoryId);
        expense.ExpenseDate.ShouldBe(Today);
        expense.Description.ShouldBe("اجاره مهر");
        expense.ReferenceNumber.ShouldBe("12345");
        expense.RecordedByUserId.ShouldBe(UserId);
        expense.IsVoided.ShouldBeFalse();
        expense.VoidedAt.ShouldBeNull();
        expense.VoidReason.ShouldBeNull();
        expense.VoidedByUserId.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Record_BlankReferenceNumber_StoresNull(string? referenceNumber)
    {
        Record(referenceNumber: referenceNumber).Value.ReferenceNumber.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Record_AmountNotPositive_Fails(decimal amount)
    {
        Record(amount: amount).Error.ShouldBe(ExpenseErrors.AmountNotPositive);
    }

    [Fact]
    public void Record_AmountWithThreeDecimals_Fails()
    {
        Record(amount: 1_000.001m).Error.ShouldBe(ExpenseErrors.AmountTooManyDecimals);
    }

    [Fact]
    public void Record_AmountAboveTheColumnLimit_Fails()
    {
        Record(amount: Expense.MaxAmount + 0.01m).Error.ShouldBe(ExpenseErrors.AmountTooLarge);
    }

    [Fact]
    public void Record_EmptyCategory_Fails()
    {
        Record(categoryId: Guid.Empty).Error.ShouldBe(ExpenseErrors.CategoryRequired);
    }

    [Fact]
    public void Record_DatedToday_Succeeds()
    {
        Record(expenseDate: Today).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Record_DatedTomorrow_FailsWithDateInFuture()
    {
        Record(expenseDate: Today.AddDays(1)).Error.ShouldBe(ExpenseErrors.DateInFuture);
    }

    [Fact]
    public void Record_DatedYearsAgo_Succeeds()
    {
        // BUSINESS_RULES.md §9: any past date, so an old bill can still be entered.
        Record(expenseDate: Today.AddYears(-5)).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Record_BlankDescription_Fails(string description)
    {
        Record(description: description).Error.ShouldBe(ExpenseErrors.DescriptionRequired);
    }

    [Fact]
    public void Record_DescriptionOverTheLimit_Fails()
    {
        Record(description: new string('ا', Expense.DescriptionMaxLength + 1))
            .Error.ShouldBe(ExpenseErrors.DescriptionTooLong);
    }

    [Fact]
    public void Record_ReferenceNumberOverTheLimit_Fails()
    {
        Record(referenceNumber: new string('1', Expense.ReferenceNumberMaxLength + 1))
            .Error.ShouldBe(ExpenseErrors.ReferenceNumberTooLong);
    }

    // ---- Update ----

    [Fact]
    public void Update_ValidInput_ReplacesTheFieldsAndKeepsWhoRecordedIt()
    {
        var expense = Record().Value;

        expense.Update(7_500m, OtherCategoryId, Today.AddDays(-3), "قبض برق", "987", Today).IsSuccess.ShouldBeTrue();

        expense.Amount.ShouldBe(7_500m);
        expense.CategoryId.ShouldBe(OtherCategoryId);
        expense.ExpenseDate.ShouldBe(Today.AddDays(-3));
        expense.Description.ShouldBe("قبض برق");
        expense.ReferenceNumber.ShouldBe("987");
        expense.RecordedByUserId.ShouldBe(UserId);
    }

    [Fact]
    public void Update_InvalidField_FailsAndLeavesEveryFieldAsItWas()
    {
        var expense = Record(amount: 1_000m, description: "آب").Value;

        expense.Update(2_000m, OtherCategoryId, Today, " ", null, Today).Error.ShouldBe(ExpenseErrors.DescriptionRequired);

        expense.Amount.ShouldBe(1_000m);
        expense.CategoryId.ShouldBe(CategoryId);
        expense.Description.ShouldBe("آب");
    }

    [Fact]
    public void Update_Voided_FailsWithAlreadyVoided()
    {
        var expense = Record(amount: 1_000m).Value;
        expense.Void("ثبت اشتباه", Now, UserId);

        expense.Update(2_000m, CategoryId, Today, "آب", null, Today).Error.ShouldBe(ExpenseErrors.AlreadyVoided);

        expense.Amount.ShouldBe(1_000m);
    }

    // ---- Void ----

    [Fact]
    public void Void_WithReason_RecordsWhenWhyAndWho()
    {
        var expense = Record().Value;
        var voider = Guid.NewGuid();

        expense.Void("  ثبت اشتباه ", Now, voider).IsSuccess.ShouldBeTrue();

        expense.IsVoided.ShouldBeTrue();
        expense.VoidedAt.ShouldBe(Now);
        expense.VoidReason.ShouldBe("ثبت اشتباه");
        expense.VoidedByUserId.ShouldBe(voider);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Void_BlankReason_FailsAndStaysStanding(string reason)
    {
        var expense = Record().Value;

        expense.Void(reason, Now, UserId).Error.ShouldBe(ExpenseErrors.VoidReasonRequired);

        expense.IsVoided.ShouldBeFalse();
    }

    [Fact]
    public void Void_ReasonOverTheLimit_Fails()
    {
        var expense = Record().Value;

        expense.Void(new string('ا', Expense.VoidReasonMaxLength + 1), Now, UserId)
            .Error.ShouldBe(ExpenseErrors.VoidReasonTooLong);
    }

    [Fact]
    public void Void_AlreadyVoided_FailsAndKeepsTheFirstReason()
    {
        var expense = Record().Value;
        expense.Void("ثبت اشتباه", Now, UserId);

        expense.Void("دلیل دیگر", Now.AddHours(1), Guid.NewGuid()).Error.ShouldBe(ExpenseErrors.AlreadyVoided);

        expense.VoidReason.ShouldBe("ثبت اشتباه");
        expense.VoidedAt.ShouldBe(Now);
    }

    private static Result<Expense> Record(
        decimal amount = 1_000_000m,
        Guid? categoryId = null,
        DateOnly? expenseDate = null,
        string description = "اجاره",
        string? referenceNumber = null) =>
            Expense.Record(
                amount, categoryId ?? CategoryId, expenseDate ?? Today, description, referenceNumber, UserId, Today);
}
