using Gym.Domain.Payments;

namespace Gym.Domain.Tests.Payments;

/// <summary>BUSINESS_RULES.md §12 <i>History</i>: Staff read payments of today and the 3 days before it.</summary>
public sealed class PaymentHistoryWindowTests
{
    /// <summary>1405/07/10.</summary>
    private static readonly DateOnly Today = new(2026, 10, 2);

    [Fact]
    public void EarliestForStaff_Today_IsThreeDaysBefore()
    {
        // 1405/07/10 → 07/07.
        PaymentHistoryWindow.EarliestForStaff(Today).ShouldBe(new DateOnly(2026, 9, 29));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void Check_StaffStartingWithinTheLastThreeDays_Succeeds(int daysBack)
    {
        PaymentHistoryWindow.Check(Today.AddDays(-daysBack), isOwner: false, Today).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Check_StaffStartingFourDaysBack_FailsWithHistoryTooFarBack()
    {
        var result = PaymentHistoryWindow.Check(Today.AddDays(-4), isOwner: false, Today);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(PaymentErrors.HistoryTooFarBack);
    }

    [Fact]
    public void Check_StaffWithNoStart_FailsWithHistoryTooFarBack()
    {
        // No start is unbounded, which reaches every payment ever taken.
        PaymentHistoryWindow.Check(from: null, isOwner: false, Today).Error.ShouldBe(PaymentErrors.HistoryTooFarBack);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-4)]
    [InlineData(-400)]
    public void Check_Owner_AlwaysSucceeds(int? daysBack)
    {
        DateOnly? from = daysBack is { } days ? Today.AddDays(days) : null;

        PaymentHistoryWindow.Check(from, isOwner: true, Today).IsSuccess.ShouldBeTrue();
    }
}
