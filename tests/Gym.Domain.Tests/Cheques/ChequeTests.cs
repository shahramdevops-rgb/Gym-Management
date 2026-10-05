using Gym.Domain.Cheques;
using Gym.Domain.Common;

namespace Gym.Domain.Tests.Cheques;

/// <summary>BUSINESS_RULES.md §9 <i>Cheques</i>, as far as one row can answer it.</summary>
public sealed class ChequeTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 10, 5);
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 10, 0, 0, TimeSpan.Zero);

    // ---- Register ----

    [Fact]
    public void Register_ValidInput_SetsAllFieldsAndIsPending()
    {
        var cheque = Register(amount: 50_000_000m, payee: "  فروشگاه تجهیزات ورزشی ", description: " قسط دوم تردمیل ").Value;

        cheque.Amount.ShouldBe(50_000_000m);
        cheque.DueDate.ShouldBe(Today);
        cheque.Payee.ShouldBe("فروشگاه تجهیزات ورزشی");
        cheque.Description.ShouldBe("قسط دوم تردمیل");
        cheque.RegisteredByUserId.ShouldBe(UserId);
        cheque.IsPending.ShouldBeTrue();
        cheque.IsPassed.ShouldBeFalse();
        cheque.IsCancelled.ShouldBeFalse();
        cheque.PassedAt.ShouldBeNull();
        cheque.CancelledAt.ShouldBeNull();
        cheque.CancelReason.ShouldBeNull();
    }

    [Fact]
    public void Register_DatedLongAgo_Succeeds()
    {
        // An old cheque can still be entered late (§9 Cheques).
        Register(dueDate: Today.AddYears(-1)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Register_DatedAYearAhead_Succeeds()
    {
        Register(dueDate: Today.AddYears(1)).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Register_AmountNotPositive_Fails(decimal amount)
    {
        Register(amount: amount).Error.ShouldBe(ChequeErrors.AmountNotPositive);
    }

    [Fact]
    public void Register_AmountWithThreeDecimals_Fails()
    {
        Register(amount: 1_000.001m).Error.ShouldBe(ChequeErrors.AmountTooManyDecimals);
    }

    [Fact]
    public void Register_AmountAboveTheColumnLimit_Fails()
    {
        Register(amount: Cheque.MaxAmount + 0.01m).Error.ShouldBe(ChequeErrors.AmountTooLarge);
    }

    [Fact]
    public void Register_AmountAtTheColumnLimit_Succeeds()
    {
        Register(amount: Cheque.MaxAmount).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_BlankPayee_Fails(string payee)
    {
        Register(payee: payee).Error.ShouldBe(ChequeErrors.PayeeRequired);
    }

    [Fact]
    public void Register_PayeeAtTheLimitAfterTrimming_Succeeds()
    {
        Register(payee: "  " + new string('ب', Cheque.PayeeMaxLength) + "  ").IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Register_PayeeOverTheLimit_Fails()
    {
        Register(payee: new string('ب', Cheque.PayeeMaxLength + 1)).Error.ShouldBe(ChequeErrors.PayeeTooLong);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_BlankDescription_Fails(string description)
    {
        Register(description: description).Error.ShouldBe(ChequeErrors.DescriptionRequired);
    }

    [Fact]
    public void Register_DescriptionAtTheLimit_Succeeds()
    {
        Register(description: new string('ت', Cheque.DescriptionMaxLength)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Register_DescriptionOverTheLimit_Fails()
    {
        Register(description: new string('ت', Cheque.DescriptionMaxLength + 1))
            .Error.ShouldBe(ChequeErrors.DescriptionTooLong);
    }

    // ---- Update ----

    [Fact]
    public void Update_Pending_ReplacesEveryTypedFieldAndKeepsWhoRegistered()
    {
        var cheque = Register().Value;

        var result = cheque.Update(70_000_000m, Today.AddDays(30), " بانک‌دار ", " قسط سوم ");

        result.IsSuccess.ShouldBeTrue();
        cheque.Amount.ShouldBe(70_000_000m);
        cheque.DueDate.ShouldBe(Today.AddDays(30));
        cheque.Payee.ShouldBe("بانک‌دار");
        cheque.Description.ShouldBe("قسط سوم");
        cheque.RegisteredByUserId.ShouldBe(UserId);
    }

    [Fact]
    public void Update_InvalidField_FailsAndLeavesTheChequeAsItWas()
    {
        var cheque = Register(amount: 50_000_000m, payee: "الف", description: "ب").Value;

        cheque.Update(60_000_000m, Today.AddDays(3), "ج", " ").Error.ShouldBe(ChequeErrors.DescriptionRequired);

        cheque.Amount.ShouldBe(50_000_000m);
        cheque.DueDate.ShouldBe(Today);
        cheque.Payee.ShouldBe("الف");
        cheque.Description.ShouldBe("ب");
    }

    [Fact]
    public void Update_Passed_FailsWithAlreadyPassed()
    {
        var cheque = Register().Value;
        cheque.MarkPassed(Today, Now, UserId).IsSuccess.ShouldBeTrue();

        cheque.Update(1m, Today, "الف", "ب").Error.ShouldBe(ChequeErrors.AlreadyPassed);
    }

    [Fact]
    public void Update_Cancelled_FailsWithAlreadyCancelled()
    {
        var cheque = Register().Value;
        cheque.Cancel("اشتباه ثبت شد", Now, UserId).IsSuccess.ShouldBeTrue();

        cheque.Update(1m, Today, "الف", "ب").Error.ShouldBe(ChequeErrors.AlreadyCancelled);
    }

    // ---- MarkPassed ----

    [Fact]
    public void MarkPassed_OnItsDate_SetsTheMomentAndTheUser()
    {
        var cheque = Register(dueDate: Today).Value;

        var result = cheque.MarkPassed(Today, Now, OtherUserId);

        result.IsSuccess.ShouldBeTrue();
        cheque.IsPassed.ShouldBeTrue();
        cheque.IsPending.ShouldBeFalse();
        cheque.PassedAt.ShouldBe(Now);
        cheque.PassedByUserId.ShouldBe(OtherUserId);
    }

    [Fact]
    public void MarkPassed_AfterItsDate_Succeeds()
    {
        Register(dueDate: Today.AddDays(-20)).Value.MarkPassed(Today, Now, UserId).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void MarkPassed_TheDayBeforeItsDate_FailsWithNotDueYet()
    {
        var cheque = Register(dueDate: Today.AddDays(1)).Value;

        cheque.MarkPassed(Today, Now, UserId).Error.ShouldBe(ChequeErrors.NotDueYet);

        cheque.IsPending.ShouldBeTrue();
    }

    [Fact]
    public void MarkPassed_Twice_FailsWithAlreadyPassedAndKeepsTheFirstMark()
    {
        var cheque = Register().Value;
        cheque.MarkPassed(Today, Now, UserId).IsSuccess.ShouldBeTrue();

        cheque.MarkPassed(Today, Now.AddHours(1), OtherUserId).Error.ShouldBe(ChequeErrors.AlreadyPassed);

        cheque.PassedAt.ShouldBe(Now);
        cheque.PassedByUserId.ShouldBe(UserId);
    }

    [Fact]
    public void MarkPassed_Cancelled_FailsWithAlreadyCancelled()
    {
        var cheque = Register().Value;
        cheque.Cancel("پس گرفته شد", Now, UserId).IsSuccess.ShouldBeTrue();

        cheque.MarkPassed(Today, Now, UserId).Error.ShouldBe(ChequeErrors.AlreadyCancelled);

        cheque.IsPassed.ShouldBeFalse();
    }

    // ---- Cancel ----

    [Fact]
    public void Cancel_Pending_KeepsTheTrimmedReasonTheMomentAndTheUser()
    {
        var cheque = Register(dueDate: Today.AddDays(10)).Value;

        var result = cheque.Cancel("  از فروشنده پس گرفته شد ", Now, OtherUserId);

        result.IsSuccess.ShouldBeTrue();
        cheque.IsCancelled.ShouldBeTrue();
        cheque.IsPending.ShouldBeFalse();
        cheque.CancelReason.ShouldBe("از فروشنده پس گرفته شد");
        cheque.CancelledAt.ShouldBe(Now);
        cheque.CancelledByUserId.ShouldBe(OtherUserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Cancel_BlankReason_FailsAndStaysPending(string reason)
    {
        var cheque = Register().Value;

        cheque.Cancel(reason, Now, UserId).Error.ShouldBe(ChequeErrors.CancelReasonRequired);

        cheque.IsPending.ShouldBeTrue();
    }

    [Fact]
    public void Cancel_ReasonOverTheLimit_Fails()
    {
        Register().Value.Cancel(new string('ر', Cheque.CancelReasonMaxLength + 1), Now, UserId)
            .Error.ShouldBe(ChequeErrors.CancelReasonTooLong);
    }

    [Fact]
    public void Cancel_ReasonAtTheLimit_Succeeds()
    {
        Register().Value.Cancel(new string('ر', Cheque.CancelReasonMaxLength), Now, UserId).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Cancel_Twice_FailsWithAlreadyCancelledAndKeepsTheFirstReason()
    {
        var cheque = Register().Value;
        cheque.Cancel("اول", Now, UserId).IsSuccess.ShouldBeTrue();

        cheque.Cancel("دوم", Now, UserId).Error.ShouldBe(ChequeErrors.AlreadyCancelled);

        cheque.CancelReason.ShouldBe("اول");
    }

    [Fact]
    public void Cancel_Passed_FailsWithAlreadyPassed()
    {
        var cheque = Register().Value;
        cheque.MarkPassed(Today, Now, UserId).IsSuccess.ShouldBeTrue();

        cheque.Cancel("دیر شد", Now, UserId).Error.ShouldBe(ChequeErrors.AlreadyPassed);

        cheque.IsCancelled.ShouldBeFalse();
    }

    private static Result<Cheque> Register(
        decimal amount = 50_000_000m,
        DateOnly? dueDate = null,
        string payee = "فروشگاه تجهیزات",
        string description = "قسط تردمیل") =>
        Cheque.Register(amount, dueDate ?? Today, payee, description, UserId);
}
