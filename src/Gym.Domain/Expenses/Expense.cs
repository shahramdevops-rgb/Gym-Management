using Gym.Domain.Common;

namespace Gym.Domain.Expenses;

/// <summary>
/// Money the gym paid out: the rent, a salary, a crate of water for the cafe (BUSINESS_RULES.md §9).
/// </summary>
/// <remarks>
/// <para>
/// <b>Editable, then final.</b> Unlike a payment, an expense can be edited while it stands: it is
/// the Owner's own note of what went out, nobody else's balance depends on it, and the audit log
/// keeps every earlier version. Once voided it is final, because a voided row that could still
/// change would make the history arguable; a correction is a fresh expense.
/// </para>
/// <para>
/// <b>Never deleted.</b> <see cref="Void"/> keeps the row with a reason (§5: financial records
/// are never deleted), and the reports leave voided rows out.
/// </para>
/// <para>
/// Whether <see cref="CategoryId"/> names a real category is a question about another table, so
/// the handler asks it and the foreign key is the safety net.
/// </para>
/// </remarks>
public sealed class Expense : Entity
{
    /// <summary>The column is <c>numeric(18,2)</c>, matching every other money column.</summary>
    public const int AmountDecimals = 2;

    public const decimal MaxAmount = 9_999_999_999_999_999.99m;

    public const int DescriptionMaxLength = 500;

    public const int ReferenceNumberMaxLength = 100;

    public const int VoidReasonMaxLength = 500;

    // For EF Core.
    private Expense()
    {
    }

    public decimal Amount { get; private set; }

    public Guid CategoryId { get; private set; }

    /// <summary>A business date in the gym's time zone: the day the money went out, for reports.</summary>
    public DateOnly ExpenseDate { get; private set; }

    public string Description { get; private set; } = string.Empty;

    /// <summary>An invoice or transfer number, when there is one.</summary>
    public string? ReferenceNumber { get; private set; }

    public Guid RecordedByUserId { get; private set; }

    /// <summary>A moment (UTC); <c>null</c> while the expense still stands.</summary>
    public DateTimeOffset? VoidedAt { get; private set; }

    /// <summary>Required whenever <see cref="VoidedAt"/> is set, and null otherwise.</summary>
    public string? VoidReason { get; private set; }

    public Guid? VoidedByUserId { get; private set; }

    /// <summary>Postgres <c>xmin</c>: two edits, or an edit and a void, cannot both land.</summary>
    public uint Version { get; private set; }

    public bool IsVoided => VoidedAt is not null;

    /// <param name="today">
    /// The gym's today. An expense cannot be dated after it, and which day that is belongs to the
    /// application, not to a clock this entity reads for itself.
    /// </param>
    public static Result<Expense> Record(
        decimal amount,
        Guid categoryId,
        DateOnly expenseDate,
        string description,
        string? referenceNumber,
        Guid recordedByUserId,
        DateOnly today)
    {
        var expense = new Expense { RecordedByUserId = recordedByUserId };
        var result = expense.Apply(amount, categoryId, expenseDate, description, referenceNumber, today);

        return result.IsSuccess ? expense : Result.Failure<Expense>(result.Error);
    }

    /// <summary>
    /// Replaces every field the Owner typed. <see cref="RecordedByUserId"/> stays: it says who
    /// entered the expense, and the audit log says who changed it.
    /// </summary>
    public Result Update(
        decimal amount, Guid categoryId, DateOnly expenseDate, string description, string? referenceNumber, DateOnly today)
    {
        if (IsVoided)
        {
            return Result.Failure(ExpenseErrors.AlreadyVoided);
        }

        return Apply(amount, categoryId, expenseDate, description, referenceNumber, today);
    }

    /// <summary>
    /// Takes the expense out of the reports without erasing it (BUSINESS_RULES.md §9).
    /// </summary>
    public Result Void(string reason, DateTimeOffset now, Guid voidedByUserId)
    {
        ArgumentNullException.ThrowIfNull(reason);

        if (IsVoided)
        {
            return Result.Failure(ExpenseErrors.AlreadyVoided);
        }

        var cleanReason = reason.Trim();
        if (cleanReason.Length == 0)
        {
            return Result.Failure(ExpenseErrors.VoidReasonRequired);
        }

        if (cleanReason.Length > VoidReasonMaxLength)
        {
            return Result.Failure(ExpenseErrors.VoidReasonTooLong);
        }

        VoidedAt = now;
        VoidReason = cleanReason;
        VoidedByUserId = voidedByUserId;

        return Result.Success();
    }

    /// <summary>
    /// The same money rule as <see cref="Payments.Payment.CheckAmount"/>, with this feature's own
    /// error codes so the Persian message names the field the Owner was typing into. Also used by
    /// the validator, so the form hears the same answer as the entity.
    /// </summary>
    public static Error? CheckAmount(decimal amount)
    {
        if (amount <= 0)
        {
            return ExpenseErrors.AmountNotPositive;
        }

        if (amount > MaxAmount)
        {
            return ExpenseErrors.AmountTooLarge;
        }

        return decimal.Round(amount, AmountDecimals) != amount ? ExpenseErrors.AmountTooManyDecimals : null;
    }

    /// <summary>Checks everything before changing anything, so a refused edit leaves the row as it was.</summary>
    private Result Apply(
        decimal amount, Guid categoryId, DateOnly expenseDate, string description, string? referenceNumber, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(description);

        var amountError = CheckAmount(amount);
        if (amountError is not null)
        {
            return Result.Failure(amountError);
        }

        if (categoryId == Guid.Empty)
        {
            return Result.Failure(ExpenseErrors.CategoryRequired);
        }

        if (expenseDate > today)
        {
            return Result.Failure(ExpenseErrors.DateInFuture);
        }

        var cleanDescription = description.Trim();
        if (cleanDescription.Length == 0)
        {
            return Result.Failure(ExpenseErrors.DescriptionRequired);
        }

        if (cleanDescription.Length > DescriptionMaxLength)
        {
            return Result.Failure(ExpenseErrors.DescriptionTooLong);
        }

        var cleanReference = string.IsNullOrWhiteSpace(referenceNumber) ? null : referenceNumber.Trim();
        if (cleanReference?.Length > ReferenceNumberMaxLength)
        {
            return Result.Failure(ExpenseErrors.ReferenceNumberTooLong);
        }

        Amount = amount;
        CategoryId = categoryId;
        ExpenseDate = expenseDate;
        Description = cleanDescription;
        ReferenceNumber = cleanReference;

        return Result.Success();
    }
}
