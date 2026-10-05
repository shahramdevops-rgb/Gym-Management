using Gym.Domain.Common;
using Gym.Domain.Expenses;
using Gym.Domain.Payables;

namespace Gym.Domain.Tests.Payables;

/// <summary>BUSINESS_RULES.md §9 <i>Cheques and instalments</i>, as far as one row can answer it.</summary>
public sealed class PayableTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 10, 5);
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 10, 0, 0, TimeSpan.Zero);

    // ---- Register ----

    [Fact]
    public void Register_ValidCheque_SetsAllFieldsAndIsPending()
    {
        var cheque = Register(amount: 50_000_000m, payee: "  فروشگاه تجهیزات ورزشی ", description: " تردمیل ").Value;

        cheque.Kind.ShouldBe(PayableKind.Cheque);
        cheque.Amount.ShouldBe(50_000_000m);
        cheque.DueDate.ShouldBe(Today);
        cheque.Payee.ShouldBe("فروشگاه تجهیزات ورزشی");
        cheque.Description.ShouldBe("تردمیل");
        cheque.CategoryId.ShouldBe(CategoryId);
        cheque.InstallmentNumber.ShouldBeNull();
        cheque.InstallmentCount.ShouldBeNull();
        cheque.RegisteredByUserId.ShouldBe(UserId);
        cheque.IsPending.ShouldBeTrue();
        cheque.IsPaid.ShouldBeFalse();
        cheque.IsCancelled.ShouldBeFalse();
        cheque.PaidAt.ShouldBeNull();
        cheque.CancelledAt.ShouldBeNull();
        cheque.CancelReason.ShouldBeNull();
    }

    [Fact]
    public void Register_ValidInstallment_KeepsItsNumbers()
    {
        var installment = RegisterInstallment(number: 3, count: 12).Value;

        installment.Kind.ShouldBe(PayableKind.Installment);
        installment.InstallmentNumber.ShouldBe(3);
        installment.InstallmentCount.ShouldBe(12);
    }

    [Fact]
    public void Register_DatedLongAgo_Succeeds()
    {
        // One written long ago can still be entered late (§9).
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
        Register(amount: amount).Error.ShouldBe(PayableErrors.AmountNotPositive);
    }

    [Fact]
    public void Register_AmountWithThreeDecimals_Fails()
    {
        Register(amount: 1_000.001m).Error.ShouldBe(PayableErrors.AmountTooManyDecimals);
    }

    [Fact]
    public void Register_AmountAboveTheColumnLimit_Fails()
    {
        Register(amount: Payable.MaxAmount + 0.01m).Error.ShouldBe(PayableErrors.AmountTooLarge);
    }

    [Fact]
    public void Register_AmountAtTheColumnLimit_Succeeds()
    {
        Register(amount: Payable.MaxAmount).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_BlankPayee_Fails(string payee)
    {
        Register(payee: payee).Error.ShouldBe(PayableErrors.PayeeRequired);
    }

    [Fact]
    public void Register_PayeeAtTheLimitAfterTrimming_Succeeds()
    {
        Register(payee: "  " + new string('ب', Payable.PayeeMaxLength) + "  ").IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Register_PayeeOverTheLimit_Fails()
    {
        Register(payee: new string('ب', Payable.PayeeMaxLength + 1)).Error.ShouldBe(PayableErrors.PayeeTooLong);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_BlankDescription_Fails(string description)
    {
        Register(description: description).Error.ShouldBe(PayableErrors.DescriptionRequired);
    }

    [Fact]
    public void Register_DescriptionAtTheLimit_Succeeds()
    {
        Register(description: new string('ت', Payable.DescriptionMaxLength)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Register_DescriptionOverTheLimit_Fails()
    {
        Register(description: new string('ت', Payable.DescriptionMaxLength + 1))
            .Error.ShouldBe(PayableErrors.DescriptionTooLong);
    }

    [Fact]
    public void Register_NoCategory_Fails()
    {
        Payable.Register(PayableKind.Cheque, 1m, Today, "الف", "ب", Guid.Empty, null, null, UserId)
            .Error.ShouldBe(PayableErrors.CategoryRequired);
    }

    [Theory]
    [InlineData(1, null)]
    [InlineData(null, 12)]
    [InlineData(1, 12)]
    public void Register_ChequeWithInstallmentNumbers_Fails(int? number, int? count)
    {
        Payable.Register(PayableKind.Cheque, 1m, Today, "الف", "ب", CategoryId, number, count, UserId)
            .Error.ShouldBe(PayableErrors.InstallmentNumbersOnlyForInstallments);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(1, null)]
    [InlineData(null, 12)]
    public void Register_InstallmentWithoutBothNumbers_Fails(int? number, int? count)
    {
        RegisterInstallment(number, count).Error.ShouldBe(PayableErrors.InstallmentNumbersRequired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(Payable.MaxInstallmentCount + 1)]
    public void Register_InstallmentCountOutOfRange_Fails(int count)
    {
        RegisterInstallment(number: 1, count: count).Error.ShouldBe(PayableErrors.InstallmentCountOutOfRange);
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(13, 12)]
    public void Register_InstallmentNumberOutOfRange_Fails(int number, int count)
    {
        RegisterInstallment(number, count).Error.ShouldBe(PayableErrors.InstallmentNumberOutOfRange);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(12, 12)]
    [InlineData(Payable.MaxInstallmentCount, Payable.MaxInstallmentCount)]
    public void Register_InstallmentNumbersAtTheEdges_Succeeds(int number, int count)
    {
        RegisterInstallment(number, count).IsSuccess.ShouldBeTrue();
    }

    // ---- Update ----

    [Fact]
    public void Update_Pending_ReplacesEveryTypedFieldAndKeepsWhoRegistered()
    {
        var payable = Register().Value;
        var otherCategory = Guid.NewGuid();

        var result = payable.Update(
            PayableKind.Installment, 70_000_000m, Today.AddDays(30), " بانک ملت ", " وام دستگاه ", otherCategory, 2, 6);

        result.IsSuccess.ShouldBeTrue();
        payable.Kind.ShouldBe(PayableKind.Installment);
        payable.Amount.ShouldBe(70_000_000m);
        payable.DueDate.ShouldBe(Today.AddDays(30));
        payable.Payee.ShouldBe("بانک ملت");
        payable.Description.ShouldBe("وام دستگاه");
        payable.CategoryId.ShouldBe(otherCategory);
        payable.InstallmentNumber.ShouldBe(2);
        payable.InstallmentCount.ShouldBe(6);
        payable.RegisteredByUserId.ShouldBe(UserId);
    }

    [Fact]
    public void Update_InvalidField_FailsAndLeavesItAsItWas()
    {
        var payable = Register(amount: 50_000_000m, payee: "الف", description: "ب").Value;

        payable.Update(PayableKind.Cheque, 60_000_000m, Today.AddDays(3), "ج", " ", CategoryId, null, null)
            .Error.ShouldBe(PayableErrors.DescriptionRequired);

        payable.Amount.ShouldBe(50_000_000m);
        payable.DueDate.ShouldBe(Today);
        payable.Payee.ShouldBe("الف");
        payable.Description.ShouldBe("ب");
    }

    [Fact]
    public void Update_Paid_FailsWithAlreadyPaid()
    {
        var payable = Register().Value;
        payable.MarkPaid(Today, Now, UserId).IsSuccess.ShouldBeTrue();

        payable.Update(PayableKind.Cheque, 1m, Today, "الف", "ب", CategoryId, null, null)
            .Error.ShouldBe(PayableErrors.AlreadyPaid);
    }

    [Fact]
    public void Update_Cancelled_FailsWithAlreadyCancelled()
    {
        var payable = Register().Value;
        payable.Cancel("اشتباه ثبت شد", Now, UserId).IsSuccess.ShouldBeTrue();

        payable.Update(PayableKind.Cheque, 1m, Today, "الف", "ب", CategoryId, null, null)
            .Error.ShouldBe(PayableErrors.AlreadyCancelled);
    }

    // ---- MarkPaid ----

    [Fact]
    public void MarkPaid_ChequeOnItsDate_SetsTheMomentAndTheUser()
    {
        var cheque = Register(dueDate: Today).Value;

        var result = cheque.MarkPaid(Today, Now, OtherUserId);

        result.IsSuccess.ShouldBeTrue();
        cheque.IsPaid.ShouldBeTrue();
        cheque.IsPending.ShouldBeFalse();
        cheque.PaidAt.ShouldBe(Now);
        cheque.PaidByUserId.ShouldBe(OtherUserId);
    }

    [Fact]
    public void MarkPaid_Always_RecordsItsExpenseDatedTodayInItsCategory()
    {
        var cheque = Register(amount: 45_000_000m, dueDate: Today.AddDays(-3), description: "تردمیل").Value;

        var expense = cheque.MarkPaid(Today, Now, OtherUserId).Value;

        expense.Amount.ShouldBe(45_000_000m);
        expense.CategoryId.ShouldBe(CategoryId);
        expense.ExpenseDate.ShouldBe(Today);
        expense.Description.ShouldBe("تردمیل");
        expense.ReferenceNumber.ShouldBeNull();
        expense.RecordedByUserId.ShouldBe(OtherUserId);
        expense.PayableId.ShouldBe(cheque.Id);
        expense.IsVoided.ShouldBeFalse();
    }

    [Fact]
    public void MarkPaid_ChequeAfterItsDate_Succeeds()
    {
        Register(dueDate: Today.AddDays(-20)).Value.MarkPaid(Today, Now, UserId).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void MarkPaid_ChequeTheDayBeforeItsDate_FailsWithChequeNotDueYet()
    {
        var cheque = Register(dueDate: Today.AddDays(1)).Value;

        cheque.MarkPaid(Today, Now, UserId).Error.ShouldBe(PayableErrors.ChequeNotDueYet);

        cheque.IsPending.ShouldBeTrue();
    }

    [Fact]
    public void MarkPaid_InstallmentBeforeItsDate_Succeeds()
    {
        // An instalment can be paid early (§9); only a cheque waits for its date.
        var installment = RegisterInstallment(1, 12, dueDate: Today.AddMonths(2)).Value;

        var expense = installment.MarkPaid(Today, Now, UserId).Value;

        installment.IsPaid.ShouldBeTrue();
        expense.ExpenseDate.ShouldBe(Today);
    }

    [Fact]
    public void MarkPaid_Twice_FailsWithAlreadyPaidAndKeepsTheFirstMark()
    {
        var payable = Register().Value;
        payable.MarkPaid(Today, Now, UserId).IsSuccess.ShouldBeTrue();

        payable.MarkPaid(Today, Now.AddHours(1), OtherUserId).Error.ShouldBe(PayableErrors.AlreadyPaid);

        payable.PaidAt.ShouldBe(Now);
        payable.PaidByUserId.ShouldBe(UserId);
    }

    [Fact]
    public void MarkPaid_Cancelled_FailsWithAlreadyCancelled()
    {
        var payable = Register().Value;
        payable.Cancel("پس گرفته شد", Now, UserId).IsSuccess.ShouldBeTrue();

        payable.MarkPaid(Today, Now, UserId).Error.ShouldBe(PayableErrors.AlreadyCancelled);

        payable.IsPaid.ShouldBeFalse();
    }

    // ---- RevertToPending ----

    [Fact]
    public void RevertToPending_Paid_IsPendingAgainAndVoidsTheExpenseWithTheSameReason()
    {
        var payable = Register().Value;
        var expense = payable.MarkPaid(Today, Now, UserId).Value;

        var result = payable.RevertToPending(expense, "  اشتباهی زده شد ", Now.AddHours(1), OtherUserId);

        result.IsSuccess.ShouldBeTrue();
        payable.IsPending.ShouldBeTrue();
        payable.PaidAt.ShouldBeNull();
        payable.PaidByUserId.ShouldBeNull();
        expense.IsVoided.ShouldBeTrue();
        expense.VoidReason.ShouldBe("اشتباهی زده شد");
        expense.VoidedAt.ShouldBe(Now.AddHours(1));
        expense.VoidedByUserId.ShouldBe(OtherUserId);
    }

    [Fact]
    public void RevertToPending_ThenPaidAgain_RecordsAFreshExpense()
    {
        var payable = Register().Value;
        var first = payable.MarkPaid(Today, Now, UserId).Value;
        payable.RevertToPending(first, "اشتباه", Now, UserId).IsSuccess.ShouldBeTrue();

        var second = payable.MarkPaid(Today, Now.AddHours(2), UserId).Value;

        second.ShouldNotBeSameAs(first);
        second.IsVoided.ShouldBeFalse();
        first.IsVoided.ShouldBeTrue();
    }

    [Fact]
    public void RevertToPending_Pending_FailsWithNotPaid()
    {
        var payable = Register().Value;
        var otherPayable = Register().Value;
        var expense = otherPayable.MarkPaid(Today, Now, UserId).Value;

        payable.RevertToPending(expense, "اشتباه", Now, UserId).Error.ShouldBe(PayableErrors.NotPaid);

        expense.IsVoided.ShouldBeFalse();
    }

    [Fact]
    public void RevertToPending_Cancelled_FailsWithAlreadyCancelled()
    {
        var payable = Register().Value;
        payable.Cancel("پس گرفته شد", Now, UserId).IsSuccess.ShouldBeTrue();
        var expense = Register().Value.MarkPaid(Today, Now, UserId).Value;

        payable.RevertToPending(expense, "اشتباه", Now, UserId).Error.ShouldBe(PayableErrors.AlreadyCancelled);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RevertToPending_BlankReason_FailsAndStaysPaid(string reason)
    {
        var payable = Register().Value;
        var expense = payable.MarkPaid(Today, Now, UserId).Value;

        payable.RevertToPending(expense, reason, Now, UserId).Error.ShouldBe(PayableErrors.RevertReasonRequired);

        payable.IsPaid.ShouldBeTrue();
        expense.IsVoided.ShouldBeFalse();
    }

    [Fact]
    public void RevertToPending_ReasonOverTheLimit_Fails()
    {
        var payable = Register().Value;
        var expense = payable.MarkPaid(Today, Now, UserId).Value;

        payable.RevertToPending(expense, new string('ر', Payable.ReasonMaxLength + 1), Now, UserId)
            .Error.ShouldBe(PayableErrors.RevertReasonTooLong);
    }

    [Fact]
    public void RevertToPending_AnotherPaymentsExpense_Throws()
    {
        var payable = Register().Value;
        payable.MarkPaid(Today, Now, UserId).IsSuccess.ShouldBeTrue();
        var otherExpense = Register().Value.MarkPaid(Today, Now, UserId).Value;

        Should.Throw<ArgumentException>(() => payable.RevertToPending(otherExpense, "اشتباه", Now, UserId));
    }

    // ---- Cancel ----

    [Fact]
    public void Cancel_Pending_KeepsTheTrimmedReasonTheMomentAndTheUser()
    {
        var payable = Register(dueDate: Today.AddDays(10)).Value;

        var result = payable.Cancel("  از فروشنده پس گرفته شد ", Now, OtherUserId);

        result.IsSuccess.ShouldBeTrue();
        payable.IsCancelled.ShouldBeTrue();
        payable.IsPending.ShouldBeFalse();
        payable.CancelReason.ShouldBe("از فروشنده پس گرفته شد");
        payable.CancelledAt.ShouldBe(Now);
        payable.CancelledByUserId.ShouldBe(OtherUserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Cancel_BlankReason_FailsAndStaysPending(string reason)
    {
        var payable = Register().Value;

        payable.Cancel(reason, Now, UserId).Error.ShouldBe(PayableErrors.CancelReasonRequired);

        payable.IsPending.ShouldBeTrue();
    }

    [Fact]
    public void Cancel_ReasonOverTheLimit_Fails()
    {
        Register().Value.Cancel(new string('ر', Payable.ReasonMaxLength + 1), Now, UserId)
            .Error.ShouldBe(PayableErrors.CancelReasonTooLong);
    }

    [Fact]
    public void Cancel_ReasonAtTheLimit_Succeeds()
    {
        Register().Value.Cancel(new string('ر', Payable.ReasonMaxLength), Now, UserId).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Cancel_Twice_FailsWithAlreadyCancelledAndKeepsTheFirstReason()
    {
        var payable = Register().Value;
        payable.Cancel("اول", Now, UserId).IsSuccess.ShouldBeTrue();

        payable.Cancel("دوم", Now, UserId).Error.ShouldBe(PayableErrors.AlreadyCancelled);

        payable.CancelReason.ShouldBe("اول");
    }

    [Fact]
    public void Cancel_Paid_FailsWithAlreadyPaid()
    {
        var payable = Register().Value;
        payable.MarkPaid(Today, Now, UserId).IsSuccess.ShouldBeTrue();

        payable.Cancel("دیر شد", Now, UserId).Error.ShouldBe(PayableErrors.AlreadyPaid);

        payable.IsCancelled.ShouldBeFalse();
    }

    // ---- The expense it recorded ----

    [Fact]
    public void ExpenseOfAPayment_Update_FailsWithLinkedToPayable()
    {
        var expense = Register().Value.MarkPaid(Today, Now, UserId).Value;

        expense.Update(1m, CategoryId, Today, "دیگر", null, Today).Error.ShouldBe(ExpenseErrors.LinkedToPayable);

        expense.Description.ShouldNotBe("دیگر");
    }

    [Fact]
    public void ExpenseOfAPayment_Void_FailsWithLinkedToPayable()
    {
        var expense = Register().Value.MarkPaid(Today, Now, UserId).Value;

        expense.Void("اشتباه", Now, UserId).Error.ShouldBe(ExpenseErrors.LinkedToPayable);

        expense.IsVoided.ShouldBeFalse();
    }

    private static Result<Payable> Register(
        decimal amount = 50_000_000m,
        DateOnly? dueDate = null,
        string payee = "فروشگاه تجهیزات",
        string description = "تردمیل") =>
        Payable.Register(PayableKind.Cheque, amount, dueDate ?? Today, payee, description, CategoryId, null, null, UserId);

    private static Result<Payable> RegisterInstallment(int? number, int? count, DateOnly? dueDate = null) =>
        Payable.Register(
            PayableKind.Installment, 5_000_000m, dueDate ?? Today, "بانک ملت", "وام دستگاه", CategoryId, number, count, UserId);
}
