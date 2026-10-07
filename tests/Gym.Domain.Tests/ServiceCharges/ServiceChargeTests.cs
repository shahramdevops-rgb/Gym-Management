using Gym.Domain.Common;
using Gym.Domain.ServiceCharges;

namespace Gym.Domain.Tests.ServiceCharges;

/// <summary>
/// BUSINESS_RULES.md §7 <i>Gym services</i>, as far as one row can answer it. Whether the visit is
/// open and whether anything has been paid are cross-table questions the handlers answer, so they
/// are covered by the integration tests instead.
/// </summary>
public sealed class ServiceChargeTests
{
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid AttendanceId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly ChargedOn = new(2026, 9, 23);
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Record_ValidAmount_SetsAllFieldsAndIsNotVoided()
    {
        var result = Record(10_000m);

        result.IsSuccess.ShouldBeTrue();
        var charge = result.Value;
        charge.MemberId.ShouldBe(MemberId);
        charge.AttendanceId.ShouldBe(AttendanceId);
        charge.Kind.ShouldBe(ServiceChargeKind.Cardio);
        charge.Amount.ShouldBe(10_000m);
        charge.ChargedOn.ShouldBe(ChargedOn);
        charge.RecordedByUserId.ShouldBe(UserId);
        charge.IsVoided.ShouldBeFalse();
        charge.VoidedAt.ShouldBeNull();
        charge.VoidReason.ShouldBeNull();
        charge.VoidedByUserId.ShouldBeNull();
    }

    /// <summary>
    /// The system never prices a service (§7), so the only thing it can say about an amount is
    /// that it is not an amount. Zero is refused, unlike a plan's price: a free treadmill session
    /// is not a charge, it is no charge.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Record_AmountNotPositive_Fails(decimal amount)
    {
        Record(amount).Error.ShouldBe(ServiceChargeErrors.AmountNotPositive);
    }

    [Fact]
    public void Record_AmountWithThreeDecimals_Fails()
    {
        Record(10_000.001m).Error.ShouldBe(ServiceChargeErrors.AmountTooManyDecimals);
    }

    [Fact]
    public void Record_AmountAboveTheColumnLimit_Fails()
    {
        Record(ServiceCharge.MaxAmount + 0.01m).Error.ShouldBe(ServiceChargeErrors.AmountTooLarge);
    }

    // ---- ChangeAmount ----

    [Fact]
    public void ChangeAmount_NotVoided_ReplacesTheAmount()
    {
        var charge = Recorded();

        charge.ChangeAmount(25_000m).IsSuccess.ShouldBeTrue();

        charge.Amount.ShouldBe(25_000m);
    }

    [Fact]
    public void ChangeAmount_InvalidAmount_LeavesTheOldOne()
    {
        var charge = Recorded();

        charge.ChangeAmount(0m).Error.ShouldBe(ServiceChargeErrors.AmountNotPositive);

        charge.Amount.ShouldBe(10_000m);
    }

    /// <summary>A voided charge is history: it is replaced by a fresh charge, not corrected (§5).</summary>
    [Fact]
    public void ChangeAmount_AfterVoid_Fails()
    {
        var charge = Recorded();
        charge.Void("اشتباه ثبت شد", Now, UserId);

        charge.ChangeAmount(25_000m).Error.ShouldBe(ServiceChargeErrors.AlreadyVoided);

        charge.Amount.ShouldBe(10_000m);
    }

    // ---- Void ----

    [Fact]
    public void Void_NotVoided_RecordsWhoWhenAndWhy()
    {
        var charge = Recorded();

        charge.Void("  اشتباه ثبت شد  ", Now, UserId).IsSuccess.ShouldBeTrue();

        charge.IsVoided.ShouldBeTrue();
        charge.VoidedAt.ShouldBe(Now);
        charge.VoidReason.ShouldBe("اشتباه ثبت شد");
        charge.VoidedByUserId.ShouldBe(UserId);
    }

    [Fact]
    public void Void_Twice_Fails()
    {
        var charge = Recorded();
        charge.Void("اشتباه ثبت شد", Now, UserId);

        charge.Void("دوباره", Now.AddMinutes(1), UserId).Error.ShouldBe(ServiceChargeErrors.AlreadyVoided);

        charge.VoidReason.ShouldBe("اشتباه ثبت شد");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Void_BlankReason_Fails(string reason)
    {
        Recorded().Void(reason, Now, UserId).Error.ShouldBe(ServiceChargeErrors.VoidReasonRequired);
    }

    [Fact]
    public void Void_ReasonTooLong_Fails()
    {
        var reason = new string('ا', ServiceCharge.VoidReasonMaxLength + 1);

        Recorded().Void(reason, Now, UserId).Error.ShouldBe(ServiceChargeErrors.VoidReasonTooLong);
    }

    // ---- Sale at the desk: فروشگاه items ----

    [Fact]
    public void RecordShopItem_ValidItem_StoresWhatWasSoldAndItsTotal()
    {
        var result = RecordShopItem("  دستکش  ", 3, 150_000m);

        result.IsSuccess.ShouldBeTrue();
        var charge = result.Value;
        charge.Kind.ShouldBe(ServiceChargeKind.Miscellaneous);
        charge.IsSale.ShouldBeTrue();
        charge.Description.ShouldBe("دستکش");
        charge.Quantity.ShouldBe(3);
        charge.UnitPrice.ShouldBe(150_000m);
        charge.Amount.ShouldBe(450_000m);
        charge.MemberId.ShouldBe(MemberId);
        charge.AttendanceId.ShouldBe(AttendanceId);
        charge.ChargedOn.ShouldBe(ChargedOn);
        charge.RecordedByUserId.ShouldBe(UserId);
        charge.IsVoided.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RecordShopItem_BlankName_Fails(string description)
    {
        RecordShopItem(description, 1, 10_000m).Error.ShouldBe(ServiceChargeErrors.DescriptionRequired);
    }

    [Fact]
    public void RecordShopItem_NameTooLong_Fails()
    {
        var description = new string('ا', ServiceCharge.DescriptionMaxLength + 1);

        RecordShopItem(description, 1, 10_000m).Error.ShouldBe(ServiceChargeErrors.DescriptionTooLong);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(ServiceCharge.MaxQuantity + 1)]
    public void RecordShopItem_QuantityOutOfRange_Fails(int quantity)
    {
        RecordShopItem("دستکش", quantity, 10_000m).Error.ShouldBe(ServiceChargeErrors.QuantityInvalid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RecordShopItem_UnitPriceNotPositive_Fails(decimal unitPrice)
    {
        RecordShopItem("دستکش", 1, unitPrice).Error.ShouldBe(ServiceChargeErrors.AmountNotPositive);
    }

    [Fact]
    public void RecordShopItem_UnitPriceWithThreeDecimals_Fails()
    {
        RecordShopItem("دستکش", 1, 10_000.001m).Error.ShouldBe(ServiceChargeErrors.AmountTooManyDecimals);
    }

    /// <summary>Each figure fits the column, but what they make together does not.</summary>
    [Fact]
    public void RecordShopItem_TotalAboveTheColumnLimit_Fails()
    {
        RecordShopItem("دستکش", 2, ServiceCharge.MaxAmount).Error.ShouldBe(ServiceChargeErrors.AmountTooLarge);
    }

    /// <summary>The general entry point cannot make a فروشگاه item without its name and quantity.</summary>
    [Fact]
    public void Record_MiscellaneousKind_Fails()
    {
        ServiceCharge.Record(MemberId, AttendanceId, ServiceChargeKind.Miscellaneous, 10_000m, ChargedOn, UserId)
            .Error.ShouldBe(ServiceChargeErrors.KindInvalid);
    }

    // ---- Sale at the desk: آنالیز and متفرقه ----

    /// <summary>
    /// Task 6.5.29: آنالیز is a single typed price, with no name or quantity. Task 6.5.36: متفرقه
    /// follows exactly the same rules.
    /// </summary>
    [Theory]
    [InlineData(ServiceChargeKind.Analysis)]
    [InlineData(ServiceChargeKind.Other)]
    public void Record_PriceOnlySale_StoresOnlyTheAmount(ServiceChargeKind kind)
    {
        var charge = ServiceCharge.Record(MemberId, AttendanceId, kind, 200_000m, ChargedOn, UserId).Value;

        charge.Kind.ShouldBe(kind);
        charge.IsSale.ShouldBeTrue();
        charge.Amount.ShouldBe(200_000m);
        charge.Description.ShouldBeNull();
        charge.Quantity.ShouldBeNull();
        charge.UnitPrice.ShouldBeNull();
    }

    // ---- Every sale ----

    /// <summary>§7 <i>Sale at the desk</i>: voided and entered again, never edited, like a cafe order.</summary>
    [Theory]
    [InlineData(ServiceChargeKind.Miscellaneous)]
    [InlineData(ServiceChargeKind.Analysis)]
    [InlineData(ServiceChargeKind.Other)]
    public void ChangeAmount_Sale_Fails(ServiceChargeKind kind)
    {
        var charge = (kind != ServiceChargeKind.Miscellaneous
            ? ServiceCharge.Record(MemberId, AttendanceId, kind, 10_000m, ChargedOn, UserId)
            : RecordShopItem("دستکش", 1, 10_000m)).Value;

        charge.ChangeAmount(25_000m).Error.ShouldBe(ServiceChargeErrors.SaleNotEditable);

        charge.Amount.ShouldBe(10_000m);
    }

    [Fact]
    public void Void_Sale_RecordsWhoWhenAndWhy()
    {
        var charge = RecordShopItem("دستکش", 1, 10_000m).Value;

        charge.Void("اشتباه ثبت شد", Now, UserId).IsSuccess.ShouldBeTrue();

        charge.IsVoided.ShouldBeTrue();
        charge.VoidReason.ShouldBe("اشتباه ثبت شد");
    }

    /// <summary>
    /// A guest's visit has no member to copy (BUSINESS_RULES.md §7 <i>Guest visit</i>, task 6.5.31):
    /// the charge is under the guest's name on the visit, on no account.
    /// </summary>
    [Theory]
    [InlineData(ServiceChargeKind.Cardio)]
    [InlineData(ServiceChargeKind.Analysis)]
    [InlineData(ServiceChargeKind.Other)]
    public void Record_GuestVisit_HasNoMember(ServiceChargeKind kind)
    {
        var result = ServiceCharge.Record(null, AttendanceId, kind, 10_000m, ChargedOn, UserId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.MemberId.ShouldBeNull();
        result.Value.AttendanceId.ShouldBe(AttendanceId);
    }

    [Fact]
    public void RecordShopItem_GuestVisit_HasNoMember()
    {
        var result = ServiceCharge.RecordShopItem(null, AttendanceId, "دستکش", 2, 150_000m, ChargedOn, UserId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.MemberId.ShouldBeNull();
        result.Value.Amount.ShouldBe(300_000m);
    }

    private static Result<ServiceCharge> RecordShopItem(string description, int quantity, decimal unitPrice) =>
        ServiceCharge.RecordShopItem(MemberId, AttendanceId, description, quantity, unitPrice, ChargedOn, UserId);

    private static Result<ServiceCharge> Record(decimal amount) =>
        ServiceCharge.Record(MemberId, AttendanceId, ServiceChargeKind.Cardio, amount, ChargedOn, UserId);

    private static ServiceCharge Recorded() => Record(10_000m).Value;
}
