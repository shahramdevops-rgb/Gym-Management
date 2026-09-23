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

    private static Result<ServiceCharge> Record(decimal amount) =>
        ServiceCharge.Record(MemberId, AttendanceId, ServiceChargeKind.Cardio, amount, ChargedOn, UserId);

    private static ServiceCharge Recorded() => Record(10_000m).Value;
}
