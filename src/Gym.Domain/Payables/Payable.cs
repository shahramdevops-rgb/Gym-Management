using Gym.Domain.Common;
using Gym.Domain.Expenses;

namespace Gym.Domain.Payables;

/// <summary>
/// Money the gym owes on a date: a cheque it wrote or one instalment (BUSINESS_RULES.md §9
/// <i>Cheques and instalments</i>). Not money in any report while pending; once paid, it is an
/// expense, written by <see cref="MarkPaid"/> itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>One entity, two kinds.</b> A cheque and an instalment share every field, the register, the
/// pending total and the reminder; they differ in two rules only (a cheque is not paid before its
/// date, an instalment says «قسط n از N»). Two entities would copy everything else twice.
/// </para>
/// <para>
/// <b>Three states, none stored as a column.</b> Pending, paid or cancelled follows from
/// <see cref="PaidAt"/> and <see cref="CancelledAt"/>, the same way an expense is voided by its
/// <c>VoidedAt</c>: a status column beside them could disagree with them.
/// </para>
/// <para>
/// <b>Paid can go back, cancelled cannot.</b> A payment marked by mistake goes back to pending with
/// a reason, and its expense is voided with the same reason, so the expenses always match the
/// register. A cancelled one is final; a mistake after that is a fresh one. Nothing is deleted.
/// </para>
/// </remarks>
public sealed class Payable : Entity
{
    /// <summary>The column is <c>numeric(18,2)</c>, matching every other money column.</summary>
    public const int AmountDecimals = 2;

    public const decimal MaxAmount = 9_999_999_999_999_999.99m;

    public const int PayeeMaxLength = 200;

    public const int DescriptionMaxLength = 500;

    public const int ReasonMaxLength = 500;

    /// <summary>Thirty years of monthly instalments: more is a typing slip.</summary>
    public const int MaxInstallmentCount = 360;

    // For EF Core.
    private Payable()
    {
    }

    public PayableKind Kind { get; private set; }

    public decimal Amount { get; private set; }

    /// <summary>The date written on the cheque, or the instalment's due day: a business date.</summary>
    public DateOnly DueDate { get; private set; }

    /// <summary>Who is paid: در وجه on a cheque, a bank or a seller for an instalment.</summary>
    public string Payee { get; private set; } = string.Empty;

    /// <summary>What it pays for, e.g. «تردمیل». Also the description of its expense.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>The expense category the payment is recorded under.</summary>
    public Guid CategoryId { get; private set; }

    /// <summary>The n of «قسط n از N»; <c>null</c> on a cheque.</summary>
    public int? InstallmentNumber { get; private set; }

    /// <summary>The N of «قسط n از N»; <c>null</c> on a cheque.</summary>
    public int? InstallmentCount { get; private set; }

    public Guid RegisteredByUserId { get; private set; }

    /// <summary>A moment (UTC); <c>null</c> until the Owner marks it «پاس شد» or «پرداخت شد».</summary>
    public DateTimeOffset? PaidAt { get; private set; }

    public Guid? PaidByUserId { get; private set; }

    /// <summary>A moment (UTC); <c>null</c> unless it was cancelled.</summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>Required whenever <see cref="CancelledAt"/> is set, and null otherwise.</summary>
    public string? CancelReason { get; private set; }

    public Guid? CancelledByUserId { get; private set; }

    /// <summary>Postgres <c>xmin</c>: two edits, or an edit and a mark, cannot both land.</summary>
    public uint Version { get; private set; }

    public bool IsPaid => PaidAt is not null;

    public bool IsCancelled => CancelledAt is not null;

    public bool IsPending => !IsPaid && !IsCancelled;

    /// <param name="installmentNumber">Required for an instalment, and must be null for a cheque.</param>
    /// <param name="installmentCount">Required for an instalment, and must be null for a cheque.</param>
    public static Result<Payable> Register(
        PayableKind kind,
        decimal amount,
        DateOnly dueDate,
        string payee,
        string description,
        Guid categoryId,
        int? installmentNumber,
        int? installmentCount,
        Guid registeredByUserId)
    {
        var payable = new Payable { RegisteredByUserId = registeredByUserId };
        var result = payable.Apply(
            kind, amount, dueDate, payee, description, categoryId, installmentNumber, installmentCount);

        return result.IsSuccess ? payable : Result.Failure<Payable>(result.Error);
    }

    /// <summary>
    /// Replaces every field the Owner typed, the kind included, while it is pending.
    /// <see cref="RegisteredByUserId"/> stays: the audit log says who changed it.
    /// </summary>
    public Result Update(
        PayableKind kind,
        decimal amount,
        DateOnly dueDate,
        string payee,
        string description,
        Guid categoryId,
        int? installmentNumber,
        int? installmentCount)
    {
        var final = CheckPending();
        if (final.IsFailure)
        {
            return final;
        }

        return Apply(kind, amount, dueDate, payee, description, categoryId, installmentNumber, installmentCount);
    }

    /// <summary>
    /// The money has left the gym's account: the record is paid and its expense is written
    /// (BUSINESS_RULES.md §9 <i>Cheques and instalments</i>). The caller saves both together.
    /// </summary>
    /// <param name="today">
    /// The gym's today: the expense's date, and the day a cheque must have reached. Under the Sayad
    /// system a bank does not pay a cheque early, so marking one early can only be a slip. An
    /// instalment can be paid early.
    /// </param>
    public Result<Expense> MarkPaid(DateOnly today, DateTimeOffset now, Guid paidByUserId)
    {
        var final = CheckPending();
        if (final.IsFailure)
        {
            return Result.Failure<Expense>(final.Error);
        }

        if (Kind == PayableKind.Cheque && DueDate > today)
        {
            return Result.Failure<Expense>(PayableErrors.ChequeNotDueYet);
        }

        PaidAt = now;
        PaidByUserId = paidByUserId;

        return Expense.RecordForPayable(Amount, CategoryId, today, Description, Id, paidByUserId);
    }

    /// <summary>
    /// «برگشت به در انتظار»: the payment was marked by mistake. Its expense is voided with the
    /// same reason, never deleted, and the record is pending again.
    /// </summary>
    /// <param name="expense">The standing expense <see cref="MarkPaid"/> wrote for this record.</param>
    public Result RevertToPending(Expense expense, string reason, DateTimeOffset now, Guid revertedByUserId)
    {
        ArgumentNullException.ThrowIfNull(expense);
        ArgumentNullException.ThrowIfNull(reason);

        if (IsCancelled)
        {
            return Result.Failure(PayableErrors.AlreadyCancelled);
        }

        if (!IsPaid)
        {
            return Result.Failure(PayableErrors.NotPaid);
        }

        var cleanReason = reason.Trim();
        if (cleanReason.Length == 0)
        {
            return Result.Failure(PayableErrors.RevertReasonRequired);
        }

        if (cleanReason.Length > ReasonMaxLength)
        {
            return Result.Failure(PayableErrors.RevertReasonTooLong);
        }

        // A caller passing anything else is a bug, not a business failure.
        if (expense.PayableId != Id || expense.IsVoided)
        {
            throw new ArgumentException("The expense is not the standing expense of this payment.", nameof(expense));
        }

        expense.VoidWithPayable(cleanReason, now, revertedByUserId);
        PaidAt = null;
        PaidByUserId = null;

        return Result.Success();
    }

    /// <summary>
    /// Entered by mistake, or taken back from the payee. Kept, with the reason, never deleted.
    /// </summary>
    public Result Cancel(string reason, DateTimeOffset now, Guid cancelledByUserId)
    {
        ArgumentNullException.ThrowIfNull(reason);

        var final = CheckPending();
        if (final.IsFailure)
        {
            return final;
        }

        var cleanReason = reason.Trim();
        if (cleanReason.Length == 0)
        {
            return Result.Failure(PayableErrors.CancelReasonRequired);
        }

        if (cleanReason.Length > ReasonMaxLength)
        {
            return Result.Failure(PayableErrors.CancelReasonTooLong);
        }

        CancelledAt = now;
        CancelReason = cleanReason;
        CancelledByUserId = cancelledByUserId;

        return Result.Success();
    }

    /// <summary>
    /// The same money rule as every other amount, with this feature's own error codes so the
    /// Persian message names the field the Owner was typing into. Also used by the validator.
    /// </summary>
    public static Error? CheckAmount(decimal amount)
    {
        if (amount <= 0)
        {
            return PayableErrors.AmountNotPositive;
        }

        if (amount > MaxAmount)
        {
            return PayableErrors.AmountTooLarge;
        }

        return decimal.Round(amount, AmountDecimals) != amount ? PayableErrors.AmountTooManyDecimals : null;
    }

    /// <summary>
    /// «قسط n از N» for an instalment, nothing for a cheque. Also used by the validator, so the form
    /// hears the same answer as the entity.
    /// </summary>
    public static Error? CheckInstallmentNumbers(PayableKind kind, int? installmentNumber, int? installmentCount)
    {
        if (kind == PayableKind.Cheque)
        {
            return installmentNumber is null && installmentCount is null
                ? null
                : PayableErrors.InstallmentNumbersOnlyForInstallments;
        }

        if (installmentNumber is not { } number || installmentCount is not { } count)
        {
            return PayableErrors.InstallmentNumbersRequired;
        }

        if (count < 1 || count > MaxInstallmentCount)
        {
            return PayableErrors.InstallmentCountOutOfRange;
        }

        return number < 1 || number > count ? PayableErrors.InstallmentNumberOutOfRange : null;
    }

    private Result CheckPending()
    {
        if (IsPaid)
        {
            return Result.Failure(PayableErrors.AlreadyPaid);
        }

        return IsCancelled ? Result.Failure(PayableErrors.AlreadyCancelled) : Result.Success();
    }

    /// <summary>Checks everything before changing anything, so a refused edit leaves the row as it was.</summary>
    private Result Apply(
        PayableKind kind,
        decimal amount,
        DateOnly dueDate,
        string payee,
        string description,
        Guid categoryId,
        int? installmentNumber,
        int? installmentCount)
    {
        ArgumentNullException.ThrowIfNull(payee);
        ArgumentNullException.ThrowIfNull(description);

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown kind.");
        }

        var amountError = CheckAmount(amount);
        if (amountError is not null)
        {
            return Result.Failure(amountError);
        }

        var cleanPayee = payee.Trim();
        if (cleanPayee.Length == 0)
        {
            return Result.Failure(PayableErrors.PayeeRequired);
        }

        if (cleanPayee.Length > PayeeMaxLength)
        {
            return Result.Failure(PayableErrors.PayeeTooLong);
        }

        var cleanDescription = description.Trim();
        if (cleanDescription.Length == 0)
        {
            return Result.Failure(PayableErrors.DescriptionRequired);
        }

        if (cleanDescription.Length > DescriptionMaxLength)
        {
            return Result.Failure(PayableErrors.DescriptionTooLong);
        }

        if (categoryId == Guid.Empty)
        {
            return Result.Failure(PayableErrors.CategoryRequired);
        }

        var numbersError = CheckInstallmentNumbers(kind, installmentNumber, installmentCount);
        if (numbersError is not null)
        {
            return Result.Failure(numbersError);
        }

        Kind = kind;
        Amount = amount;
        DueDate = dueDate;
        Payee = cleanPayee;
        Description = cleanDescription;
        CategoryId = categoryId;
        InstallmentNumber = installmentNumber;
        InstallmentCount = installmentCount;

        return Result.Success();
    }
}
