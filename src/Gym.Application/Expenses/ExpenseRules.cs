using FluentValidation;

using Gym.Domain.Expenses;

namespace Gym.Application.Expenses;

/// <summary>
/// The field checks the expense commands share, with the entities' error codes, so the form gets
/// a message per field before anything touches the database. The limits come from
/// <see cref="Expense"/> and <see cref="ExpenseCategory"/>, so the validator and the entity cannot
/// disagree. The one rule missing here is "not after today", which needs the gym's calendar and
/// is answered by the entity.
/// </summary>
public static class ExpenseRules
{
    public static IRuleBuilderOptions<T, string> ValidCategoryName<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithErrorCode(ExpenseCategoryErrors.NameRequired.Code)
            .WithMessage(ExpenseCategoryErrors.NameRequired.Description)
            .Must(name => name.Trim().Length <= ExpenseCategory.NameMaxLength)
            .WithErrorCode(ExpenseCategoryErrors.NameTooLong.Code)
            .WithMessage(ExpenseCategoryErrors.NameTooLong.Description);

    /// <summary>One check per error, so each failure carries its own code for the form.</summary>
    public static IRuleBuilderOptions<T, decimal> ValidAmount<T>(this IRuleBuilderInitial<T, decimal> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(amount => Expense.CheckAmount(amount) != ExpenseErrors.AmountNotPositive)
            .WithErrorCode(ExpenseErrors.AmountNotPositive.Code).WithMessage(ExpenseErrors.AmountNotPositive.Description)
            .Must(amount => Expense.CheckAmount(amount) != ExpenseErrors.AmountTooLarge)
            .WithErrorCode(ExpenseErrors.AmountTooLarge.Code).WithMessage(ExpenseErrors.AmountTooLarge.Description)
            .Must(amount => Expense.CheckAmount(amount) != ExpenseErrors.AmountTooManyDecimals)
            .WithErrorCode(ExpenseErrors.AmountTooManyDecimals.Code)
            .WithMessage(ExpenseErrors.AmountTooManyDecimals.Description);

    public static IRuleBuilderOptions<T, Guid> ValidCategoryId<T>(this IRuleBuilderInitial<T, Guid> rule) =>
        rule.NotEmpty()
            .WithErrorCode(ExpenseErrors.CategoryRequired.Code).WithMessage(ExpenseErrors.CategoryRequired.Description);

    public static IRuleBuilderOptions<T, string> ValidDescription<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(description => !string.IsNullOrWhiteSpace(description))
            .WithErrorCode(ExpenseErrors.DescriptionRequired.Code)
            .WithMessage(ExpenseErrors.DescriptionRequired.Description)
            .Must(description => description.Trim().Length <= Expense.DescriptionMaxLength)
            .WithErrorCode(ExpenseErrors.DescriptionTooLong.Code)
            .WithMessage(ExpenseErrors.DescriptionTooLong.Description);

    public static IRuleBuilderOptions<T, string?> ValidReferenceNumber<T>(this IRuleBuilderInitial<T, string?> rule) =>
        rule.Must(reference => reference is null || reference.Trim().Length <= Expense.ReferenceNumberMaxLength)
            .WithErrorCode(ExpenseErrors.ReferenceNumberTooLong.Code)
            .WithMessage(ExpenseErrors.ReferenceNumberTooLong.Description);

    public static IRuleBuilderOptions<T, string> ValidVoidReason<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithErrorCode(ExpenseErrors.VoidReasonRequired.Code)
            .WithMessage(ExpenseErrors.VoidReasonRequired.Description)
            .Must(reason => reason.Trim().Length <= Expense.VoidReasonMaxLength)
            .WithErrorCode(ExpenseErrors.VoidReasonTooLong.Code)
            .WithMessage(ExpenseErrors.VoidReasonTooLong.Description);
}
