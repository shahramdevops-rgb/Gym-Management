using Gym.Domain.Common;

namespace Gym.Domain.ServiceCharges;

/// <summary>
/// Money owed for something the member used or bought during a visit — هوازی, the treadmill, and
/// what the desk sells, فروشگاه and آنالیز (BUSINESS_RULES.md §7 <i>Gym services</i>,
/// <i>Sale at the desk</i>).
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

    /// <summary>A فروشگاه item's name; the same limit as a cafe product's name.</summary>
    public const int DescriptionMaxLength = 100;

    /// <summary>The same limit as a cafe order line: nobody buys 1,000 of one thing at the desk.</summary>
    public const int MaxQuantity = 999;

    /// <summary>How many items one «فروشگاه» sale may carry; the cafe order's limit.</summary>
    public const int MaxShopItemsPerSale = 50;

    // For EF Core.
    private ServiceCharge()
    {
    }

    /// <summary>The member of the visit. Copied from the attendance, never chosen separately.</summary>
    public Guid MemberId { get; private set; }

    public Guid AttendanceId { get; private set; }

    public ServiceChargeKind Kind { get; private set; }

    /// <summary>
    /// What is owed. For a فروشگاه item it is
    /// <see cref="UnitPrice"/> × <see cref="Quantity"/>, stored like a cafe line's total: it is
    /// what the member was charged, and a stored figure cannot be re-derived differently later.
    /// </summary>
    public decimal Amount { get; private set; }

    /// <summary>What a فروشگاه item is, as the desk typed it; <c>null</c> for every other kind.</summary>
    public string? Description { get; private set; }

    /// <summary>How many of it were sold; <c>null</c> except for a فروشگاه item.</summary>
    public int? Quantity { get; private set; }

    /// <summary>The price of one, as the desk typed it; <c>null</c> except for a فروشگاه item.</summary>
    public decimal? UnitPrice { get; private set; }

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

    /// <summary>Whether this charge is something sold at the desk (<see cref="IsSaleKind"/>).</summary>
    public bool IsSale => IsSaleKind(Kind);

    /// <summary>
    /// The kinds sold at the desk (BUSINESS_RULES.md §7 <i>Sale at the desk</i>): فروشگاه and
    /// آنالیز. Any number per visit, and voided and entered again rather than edited. Only a
    /// فروشگاه item carries a name, a quantity and a unit price (<see cref="RecordShopItem"/>);
    /// آنالیز is a single typed amount, like هوازی.
    /// </summary>
    public static bool IsSaleKind(ServiceChargeKind kind) =>
        kind is ServiceChargeKind.Miscellaneous or ServiceChargeKind.Analysis;

    /// <summary>
    /// Records a charge against a visit. Whether that visit is open, and whether it already has a
    /// live charge of this kind, are cross-table questions the caller answers (the same division
    /// <see cref="Attendances.Attendance.CheckIn"/> uses).
    /// </summary>
    public static Result<ServiceCharge> Record(
        Guid memberId, Guid attendanceId, ServiceChargeKind kind, decimal amount, DateOnly chargedOn, Guid recordedByUserId)
    {
        // A فروشگاه item needs its name and quantity, which only RecordShopItem takes.
        if (kind == ServiceChargeKind.Miscellaneous)
        {
            return Result.Failure<ServiceCharge>(ServiceChargeErrors.KindInvalid);
        }

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
    /// Records one فروشگاه item (BUSINESS_RULES.md §7 <i>Sale at the desk</i>): something the desk
    /// sold that has no product of its own, so the desk types its name, how many and the price of
    /// one. As with هوازی the system never checks the price against anything.
    /// </summary>
    public static Result<ServiceCharge> RecordShopItem(
        Guid memberId, Guid attendanceId, string description, int quantity, decimal unitPrice,
        DateOnly chargedOn, Guid recordedByUserId)
    {
        ArgumentNullException.ThrowIfNull(description);

        var cleanDescription = description.Trim();
        if (cleanDescription.Length == 0)
        {
            return Result.Failure<ServiceCharge>(ServiceChargeErrors.DescriptionRequired);
        }

        if (cleanDescription.Length > DescriptionMaxLength)
        {
            return Result.Failure<ServiceCharge>(ServiceChargeErrors.DescriptionTooLong);
        }

        if (quantity is < 1 or > MaxQuantity)
        {
            return Result.Failure<ServiceCharge>(ServiceChargeErrors.QuantityInvalid);
        }

        // The unit price obeys the money rule itself, and so does the total it makes: 999 of a
        // valid price can still overflow the column.
        var priceError = CheckAmount(unitPrice) ?? CheckAmount(unitPrice * quantity);
        if (priceError is not null)
        {
            return Result.Failure<ServiceCharge>(priceError);
        }

        return new ServiceCharge
        {
            MemberId = memberId,
            AttendanceId = attendanceId,
            Kind = ServiceChargeKind.Miscellaneous,
            Description = cleanDescription,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Amount = unitPrice * quantity,
            ChargedOn = chargedOn,
            RecordedByUserId = recordedByUserId,
        };
    }

    /// <summary>
    /// Corrects the amount while nothing has been settled. "Nothing has been paid against it" is
    /// the caller's check; a voided charge is not corrected at all, which this row does know.
    /// </summary>
    /// <remarks>
    /// A sale is never edited: it is voided with a reason and entered again, the rule a cafe order
    /// follows (§7 <i>Sale at the desk</i>, §8).
    /// </remarks>
    public Result ChangeAmount(decimal amount)
    {
        if (IsVoided)
        {
            return Result.Failure(ServiceChargeErrors.AlreadyVoided);
        }

        if (IsSale)
        {
            return Result.Failure(ServiceChargeErrors.SaleNotEditable);
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
