using Gym.Domain.Common;

namespace Gym.Domain.Cheques;

/// <summary>
/// A dated cheque the gym wrote, usually an instalment on equipment (BUSINESS_RULES.md §9
/// <i>Cheques</i>). Not an expense and not money in any report: the system only reminds the Owner,
/// who records the expense by hand on the cheque's date.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three states, none stored as a column.</b> Pending, passed or cancelled follows from
/// <see cref="PassedAt"/> and <see cref="CancelledAt"/>, the same way an expense is voided by its
/// <c>VoidedAt</c>: a status column beside them could disagree with them.
/// </para>
/// <para>
/// <b>Editable while pending, then final.</b> Passed and cancelled are both the end: a cheque that
/// could still change after the money left, or after it was taken back, would make the register
/// arguable. A mistake after that is a fresh cheque. Nothing is ever deleted.
/// </para>
/// <para>
/// <b>Not passed by its date alone.</b> A cheque past its date stays pending until the Owner marks
/// it, so the reminder to record the expense does not disappear on its own.
/// </para>
/// </remarks>
public sealed class Cheque : Entity
{
    /// <summary>The column is <c>numeric(18,2)</c>, matching every other money column.</summary>
    public const int AmountDecimals = 2;

    public const decimal MaxAmount = 9_999_999_999_999_999.99m;

    public const int PayeeMaxLength = 200;

    public const int DescriptionMaxLength = 500;

    public const int CancelReasonMaxLength = 500;

    // For EF Core.
    private Cheque()
    {
    }

    public decimal Amount { get; private set; }

    /// <summary>The date written on the cheque: a business date, the day the bank may pay it.</summary>
    public DateOnly DueDate { get; private set; }

    /// <summary>Who the cheque is made out to (در وجه).</summary>
    public string Payee { get; private set; } = string.Empty;

    /// <summary>What the cheque pays for, e.g. «قسط دوم تردمیل».</summary>
    public string Description { get; private set; } = string.Empty;

    public Guid RegisteredByUserId { get; private set; }

    /// <summary>A moment (UTC); <c>null</c> until the Owner marks the cheque «پاس شد».</summary>
    public DateTimeOffset? PassedAt { get; private set; }

    public Guid? PassedByUserId { get; private set; }

    /// <summary>A moment (UTC); <c>null</c> unless the cheque was cancelled.</summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>Required whenever <see cref="CancelledAt"/> is set, and null otherwise.</summary>
    public string? CancelReason { get; private set; }

    public Guid? CancelledByUserId { get; private set; }

    /// <summary>Postgres <c>xmin</c>: two edits, or an edit and a mark, cannot both land.</summary>
    public uint Version { get; private set; }

    public bool IsPassed => PassedAt is not null;

    public bool IsCancelled => CancelledAt is not null;

    public bool IsPending => !IsPassed && !IsCancelled;

    public static Result<Cheque> Register(
        decimal amount, DateOnly dueDate, string payee, string description, Guid registeredByUserId)
    {
        var cheque = new Cheque { RegisteredByUserId = registeredByUserId };
        var result = cheque.Apply(amount, dueDate, payee, description);

        return result.IsSuccess ? cheque : Result.Failure<Cheque>(result.Error);
    }

    /// <summary>
    /// Replaces every field the Owner typed, while the cheque is pending.
    /// <see cref="RegisteredByUserId"/> stays: the audit log says who changed it.
    /// </summary>
    public Result Update(decimal amount, DateOnly dueDate, string payee, string description)
    {
        var final = CheckPending();
        if (final.IsFailure)
        {
            return final;
        }

        return Apply(amount, dueDate, payee, description);
    }

    /// <summary>
    /// The money has left the gym's account (BUSINESS_RULES.md §9 <i>Cheques</i>).
    /// </summary>
    /// <param name="today">
    /// The gym's today. A cheque cannot be passed before its date: under the Sayad system a bank
    /// does not pay a cheque early, so marking one early can only be a slip.
    /// </param>
    public Result MarkPassed(DateOnly today, DateTimeOffset now, Guid passedByUserId)
    {
        var final = CheckPending();
        if (final.IsFailure)
        {
            return final;
        }

        if (DueDate > today)
        {
            return Result.Failure(ChequeErrors.NotDueYet);
        }

        PassedAt = now;
        PassedByUserId = passedByUserId;

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
            return Result.Failure(ChequeErrors.CancelReasonRequired);
        }

        if (cleanReason.Length > CancelReasonMaxLength)
        {
            return Result.Failure(ChequeErrors.CancelReasonTooLong);
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
            return ChequeErrors.AmountNotPositive;
        }

        if (amount > MaxAmount)
        {
            return ChequeErrors.AmountTooLarge;
        }

        return decimal.Round(amount, AmountDecimals) != amount ? ChequeErrors.AmountTooManyDecimals : null;
    }

    private Result CheckPending()
    {
        if (IsPassed)
        {
            return Result.Failure(ChequeErrors.AlreadyPassed);
        }

        return IsCancelled ? Result.Failure(ChequeErrors.AlreadyCancelled) : Result.Success();
    }

    /// <summary>Checks everything before changing anything, so a refused edit leaves the row as it was.</summary>
    private Result Apply(decimal amount, DateOnly dueDate, string payee, string description)
    {
        ArgumentNullException.ThrowIfNull(payee);
        ArgumentNullException.ThrowIfNull(description);

        var amountError = CheckAmount(amount);
        if (amountError is not null)
        {
            return Result.Failure(amountError);
        }

        var cleanPayee = payee.Trim();
        if (cleanPayee.Length == 0)
        {
            return Result.Failure(ChequeErrors.PayeeRequired);
        }

        if (cleanPayee.Length > PayeeMaxLength)
        {
            return Result.Failure(ChequeErrors.PayeeTooLong);
        }

        var cleanDescription = description.Trim();
        if (cleanDescription.Length == 0)
        {
            return Result.Failure(ChequeErrors.DescriptionRequired);
        }

        if (cleanDescription.Length > DescriptionMaxLength)
        {
            return Result.Failure(ChequeErrors.DescriptionTooLong);
        }

        Amount = amount;
        DueDate = dueDate;
        Payee = cleanPayee;
        Description = cleanDescription;

        return Result.Success();
    }
}
