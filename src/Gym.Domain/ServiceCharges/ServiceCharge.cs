using Gym.Domain.Common;

namespace Gym.Domain.ServiceCharges;

/// <summary>
/// Money owed for something the member used during a visit — today only هوازی, the treadmill
/// (BUSINESS_RULES.md §7 <i>Gym services</i>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The system does not price it.</b> The gym's rate changes without notice and staff work the
/// figure out at the desk, so <see cref="Amount"/> is whatever they typed. There is no rate
/// setting here to drift out of step with reality.
/// </para>
/// <para>
/// <b>It is per visit, not per member.</b> The same member uses the treadmill today and not
/// tomorrow, which is why this hangs off <see cref="AttendanceId"/> rather than off the member.
/// <see cref="MemberId"/> is copied from the visit so the debt query does not have to join.
/// </para>
/// <para>
/// <b>Editable, then not.</b> While the visit is open and nothing has been paid against it,
/// <see cref="ChangeAmount"/> corrects a typo — nothing has been settled. After check-out or the
/// first payment it is a financial record, so the only correction left is
/// <see cref="Void"/> plus a reason and a fresh charge if one is due (§5: financial records are
/// never edited or deleted). Both of those preconditions span other tables, so the handler checks
/// them; this entity enforces what it can see on its own row.
/// </para>
/// </remarks>
public sealed class ServiceCharge : Entity
{
    /// <summary>The column is <c>numeric(18,2)</c>, matching every other money column.</summary>
    public const int AmountDecimals = 2;

    public const decimal MaxAmount = 9_999_999_999_999_999.99m;

    public const int VoidReasonMaxLength = 500;

    // For EF Core.
    private ServiceCharge()
    {
    }

    /// <summary>The member of the visit. Copied from the attendance, never chosen separately.</summary>
    public Guid MemberId { get; private set; }

    public Guid AttendanceId { get; private set; }

    public ServiceChargeKind Kind { get; private set; }

    public decimal Amount { get; private set; }

    /// <summary>A business date in the gym's time zone: the day of the visit, for reports.</summary>
    public DateOnly ChargedOn { get; private set; }

    public Guid RecordedByUserId { get; private set; }

    /// <summary>A moment (UTC); <c>null</c> while the charge still stands.</summary>
    public DateTimeOffset? VoidedAt { get; private set; }

    /// <summary>Required whenever <see cref="VoidedAt"/> is set, and null otherwise.</summary>
    public string? VoidReason { get; private set; }

    public Guid? VoidedByUserId { get; private set; }

    /// <summary>Postgres <c>xmin</c>: two people cannot change or void the same charge at once.</summary>
    public uint Version { get; private set; }

    public bool IsVoided => VoidedAt is not null;

    /// <summary>
    /// Records a charge against a visit. Whether that visit is open, and whether it already has a
    /// live charge of this kind, are cross-table questions the caller answers (the same division
    /// <see cref="Attendances.Attendance.CheckIn"/> uses).
    /// </summary>
    public static Result<ServiceCharge> Record(
        Guid memberId, Guid attendanceId, ServiceChargeKind kind, decimal amount, DateOnly chargedOn, Guid recordedByUserId)
    {
        var amountError = CheckAmount(amount);
        if (amountError is not null)
        {
            return Result.Failure<ServiceCharge>(amountError);
        }

        return new ServiceCharge
        {
            MemberId = memberId,
            AttendanceId = attendanceId,
            Kind = kind,
            Amount = amount,
            ChargedOn = chargedOn,
            RecordedByUserId = recordedByUserId,
        };
    }

    /// <summary>
    /// Corrects the amount while nothing has been settled. "Nothing has been paid against it" is
    /// the caller's check; a voided charge is not corrected at all, which this row does know.
    /// </summary>
    public Result ChangeAmount(decimal amount)
    {
        if (IsVoided)
        {
            return Result.Failure(ServiceChargeErrors.AlreadyVoided);
        }

        var amountError = CheckAmount(amount);
        if (amountError is not null)
        {
            return Result.Failure(amountError);
        }

        Amount = amount;

        return Result.Success();
    }

    /// <summary>
    /// Undoes the charge without erasing it (BUSINESS_RULES.md §7, §5). A voided charge owes
    /// nothing, so it leaves the member's debt; money already taken against it comes back as a
    /// refund, which the caller writes because payments live in another aggregate.
    /// </summary>
    public Result Void(string reason, DateTimeOffset now, Guid voidedByUserId)
    {
        ArgumentNullException.ThrowIfNull(reason);

        if (IsVoided)
        {
            return Result.Failure(ServiceChargeErrors.AlreadyVoided);
        }

        var cleanReason = reason.Trim();
        if (cleanReason.Length == 0)
        {
            return Result.Failure(ServiceChargeErrors.VoidReasonRequired);
        }

        if (cleanReason.Length > VoidReasonMaxLength)
        {
            return Result.Failure(ServiceChargeErrors.VoidReasonTooLong);
        }

        VoidedAt = now;
        VoidReason = cleanReason;
        VoidedByUserId = voidedByUserId;

        return Result.Success();
    }

    /// <summary>
    /// The same money rule as <see cref="Payments.Payment.CheckAmount"/>, with this feature's own
    /// error codes so the Persian message names the field the user was typing into. Also used by
    /// the validator, so the form hears the same answer as the entity.
    /// </summary>
    public static Error? CheckAmount(decimal amount)
    {
        if (amount <= 0)
        {
            return ServiceChargeErrors.AmountNotPositive;
        }

        if (amount > MaxAmount)
        {
            return ServiceChargeErrors.AmountTooLarge;
        }

        return decimal.Round(amount, AmountDecimals) != amount ? ServiceChargeErrors.AmountTooManyDecimals : null;
    }
}
