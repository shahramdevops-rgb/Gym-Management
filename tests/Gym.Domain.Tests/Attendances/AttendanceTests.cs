using Gym.Domain.Attendances;

namespace Gym.Domain.Tests.Attendances;

public sealed class AttendanceTests
{
    private const int CancelWindowMinutes = 30;

    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid SubscriptionId = Guid.NewGuid();
    private static readonly DateTimeOffset CheckedInAt = new(2026, 1, 15, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public void CheckIn_WithLocker_SetsAllFieldsAndLeavesItOpen()
    {
        var lockerId = Guid.NewGuid();

        var attendance = Attendance.CheckIn(MemberId, SubscriptionId, lockerId, CheckedInAt);

        attendance.MemberId.ShouldBe(MemberId);
        attendance.SubscriptionId.ShouldBe(SubscriptionId);
        attendance.LockerId.ShouldBe(lockerId);
        attendance.ReserveSlot.ShouldBeNull();
        attendance.UsesReservePlace.ShouldBeFalse();
        attendance.CheckedInAt.ShouldBe(CheckedInAt);
        attendance.CheckedOutAt.ShouldBeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(Attendance.ReservePlaceCount)]
    public void CheckInOnReservePlace_ValidSlot_HoldsTheReservePlaceAndNoLocker(int slot)
    {
        var attendance = Attendance.CheckInOnReservePlace(MemberId, SubscriptionId, slot, CheckedInAt);

        attendance.ReserveSlot.ShouldBe(slot);
        attendance.UsesReservePlace.ShouldBeTrue();
        attendance.LockerId.ShouldBeNull();
        attendance.CheckedOutAt.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(Attendance.ReservePlaceCount + 1)]
    public void CheckInOnReservePlace_SlotOutOfRange_Throws(int slot)
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => Attendance.CheckInOnReservePlace(MemberId, SubscriptionId, slot, CheckedInAt));
    }

    // ---- MoveToLocker ----

    [Fact]
    public void MoveToLocker_OpenVisit_TakesTheNewLocker()
    {
        var attendance = OpenAttendance();
        var target = Guid.NewGuid();

        var result = attendance.MoveToLocker(target);

        result.IsSuccess.ShouldBeTrue();
        attendance.LockerId.ShouldBe(target);
        attendance.CheckedOutAt.ShouldBeNull();
    }

    [Fact]
    public void MoveToLocker_FromReservePlace_GivesUpTheReservePlace()
    {
        var attendance = Attendance.CheckInOnReservePlace(MemberId, SubscriptionId, 3, CheckedInAt);
        var target = Guid.NewGuid();

        var result = attendance.MoveToLocker(target);

        result.IsSuccess.ShouldBeTrue();
        attendance.LockerId.ShouldBe(target);
        attendance.ReserveSlot.ShouldBeNull();
        attendance.UsesReservePlace.ShouldBeFalse();
    }

    [Fact]
    public void MoveToLocker_SameLocker_ReturnsSameLocker()
    {
        var lockerId = Guid.NewGuid();
        var attendance = Attendance.CheckIn(MemberId, SubscriptionId, lockerId, CheckedInAt);

        var result = attendance.MoveToLocker(lockerId);

        result.Error.ShouldBe(AttendanceErrors.SameLocker);
        attendance.LockerId.ShouldBe(lockerId);
    }

    [Fact]
    public void MoveToLocker_ClosedVisit_ReturnsNotOpenAndKeepsTheLocker()
    {
        var lockerId = Guid.NewGuid();
        var attendance = Attendance.CheckIn(MemberId, SubscriptionId, lockerId, CheckedInAt);
        attendance.CheckOut(CheckedInAt.AddHours(1)).IsSuccess.ShouldBeTrue();

        var result = attendance.MoveToLocker(Guid.NewGuid());

        result.Error.ShouldBe(AttendanceErrors.NotOpen);
        attendance.LockerId.ShouldBe(lockerId);
    }

    // ---- CheckOut ----

    [Fact]
    public void CheckOut_WhenOpen_SetsCheckedOutAt()
    {
        var attendance = OpenAttendance();
        var checkedOutAt = CheckedInAt.AddHours(1);

        var result = attendance.CheckOut(checkedOutAt);

        result.IsSuccess.ShouldBeTrue();
        attendance.CheckedOutAt.ShouldBe(checkedOutAt);
        attendance.CancelledAt.ShouldBeNull();
    }

    [Fact]
    public void CheckOut_AlreadyCheckedOut_ReturnsNotOpen()
    {
        var attendance = OpenAttendance();
        attendance.CheckOut(CheckedInAt.AddHours(1)).IsSuccess.ShouldBeTrue();

        var result = attendance.CheckOut(CheckedInAt.AddHours(2));

        result.Error.ShouldBe(AttendanceErrors.NotOpen);
    }

    // ---- Cancel ----

    [Fact]
    public void Cancel_WithinWindow_SetsCancelledAtAndChecksOut()
    {
        var attendance = OpenAttendance();
        var now = CheckedInAt.AddMinutes(10);

        var result = attendance.Cancel(now, CancelWindowMinutes);

        result.IsSuccess.ShouldBeTrue();
        attendance.CancelledAt.ShouldBe(now);
        attendance.CheckedOutAt.ShouldBe(now);
    }

    [Fact]
    public void Cancel_AtExactWindowBoundary_Succeeds()
    {
        var attendance = OpenAttendance();
        var now = CheckedInAt.AddMinutes(CancelWindowMinutes);

        var result = attendance.Cancel(now, CancelWindowMinutes);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Cancel_PastWindow_ReturnsCancelWindowExpired()
    {
        var attendance = OpenAttendance();
        var now = CheckedInAt.AddMinutes(CancelWindowMinutes).AddSeconds(1);

        var result = attendance.Cancel(now, CancelWindowMinutes);

        result.Error.ShouldBe(AttendanceErrors.CancelWindowExpired);
        attendance.CancelledAt.ShouldBeNull();
        attendance.CheckedOutAt.ShouldBeNull();
    }

    [Fact]
    public void Cancel_AlreadyClosed_ReturnsNotOpen()
    {
        var attendance = OpenAttendance();
        attendance.CheckOut(CheckedInAt.AddMinutes(5)).IsSuccess.ShouldBeTrue();

        var result = attendance.Cancel(CheckedInAt.AddMinutes(10), CancelWindowMinutes);

        result.Error.ShouldBe(AttendanceErrors.NotOpen);
    }

    // ---- AutoClose ----

    [Fact]
    public void AutoClose_WhenOpen_SetsCheckedOutAtAndAutoClosedAt()
    {
        var attendance = OpenAttendance();
        var closedAt = CheckedInAt.AddHours(3);

        attendance.AutoClose(closedAt);

        attendance.CheckedOutAt.ShouldBe(closedAt);
        attendance.AutoClosedAt.ShouldBe(closedAt);
        attendance.CancelledAt.ShouldBeNull();
    }

    // ---- Guest visit ----

    [Fact]
    public void CheckInGuest_WithLocker_HasANameAndNoMemberOrSubscription()
    {
        var lockerId = Guid.NewGuid();

        var attendance = Attendance.CheckInGuest("  مریم احمدی  ", lockerId, CheckedInAt).Value;

        attendance.GuestName.ShouldBe("مریم احمدی");
        attendance.IsGuest.ShouldBeTrue();
        attendance.MemberId.ShouldBeNull();
        attendance.SubscriptionId.ShouldBeNull();
        attendance.LockerId.ShouldBe(lockerId);
        attendance.UsesReservePlace.ShouldBeFalse();
        attendance.CheckedOutAt.ShouldBeNull();
    }

    // ---- Cardio-only visit (BUSINESS_RULES.md §7) ----

    [Fact]
    public void CheckIn_Ordinary_IsNotCardioOnly() =>
        OpenAttendance().IsCardioOnly.ShouldBeFalse();

    [Fact]
    public void CheckInCardioOnly_WithLocker_NamesThePlanAndIsCardioOnly()
    {
        var lockerId = Guid.NewGuid();

        var attendance = Attendance.CheckInCardioOnly(MemberId, SubscriptionId, lockerId, CheckedInAt);

        attendance.IsCardioOnly.ShouldBeTrue();
        attendance.MemberId.ShouldBe(MemberId);
        attendance.SubscriptionId.ShouldBe(SubscriptionId);
        attendance.LockerId.ShouldBe(lockerId);
        attendance.IsGuest.ShouldBeFalse();
        attendance.CheckedOutAt.ShouldBeNull();
    }

    [Fact]
    public void CheckInCardioOnly_NoPlan_IsAMembersVisitWithNoSubscription()
    {
        // No plan is needed to come in for هوازی (BUSINESS_RULES.md §7 Cardio-only visit).
        var attendance = Attendance.CheckInCardioOnly(MemberId, subscriptionId: null, Guid.NewGuid(), CheckedInAt);

        attendance.IsCardioOnly.ShouldBeTrue();
        attendance.MemberId.ShouldBe(MemberId);
        attendance.SubscriptionId.ShouldBeNull();
        attendance.IsGuest.ShouldBeFalse();
    }

    [Fact]
    public void CheckOut_CardioOnlyNoPlanWithoutACardioCharge_Fails() =>
        Attendance.CheckInCardioOnly(MemberId, subscriptionId: null, Guid.NewGuid(), CheckedInAt)
            .CheckOut(CheckedInAt.AddHours(1))
            .Error.ShouldBe(AttendanceErrors.CardioChargeMissing);

    [Fact]
    public void CheckInCardioOnlyOnReservePlace_ValidSlot_HoldsTheReservePlace()
    {
        var attendance = Attendance.CheckInCardioOnlyOnReservePlace(MemberId, SubscriptionId, 2, CheckedInAt);

        attendance.IsCardioOnly.ShouldBeTrue();
        attendance.ReserveSlot.ShouldBe(2);
        attendance.LockerId.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(Attendance.ReservePlaceCount + 1)]
    public void CheckInCardioOnlyOnReservePlace_SlotOutOfRange_Throws(int slot)
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => Attendance.CheckInCardioOnlyOnReservePlace(MemberId, SubscriptionId, slot, CheckedInAt));
    }

    [Fact]
    public void CheckOut_CardioOnlyWithoutACardioCharge_FailsAndStaysOpen()
    {
        var attendance = Attendance.CheckInCardioOnly(MemberId, SubscriptionId, Guid.NewGuid(), CheckedInAt);

        var result = attendance.CheckOut(CheckedInAt.AddHours(1), hasCardioCharge: false);

        result.Error.ShouldBe(AttendanceErrors.CardioChargeMissing);
        attendance.CheckedOutAt.ShouldBeNull();
    }

    [Fact]
    public void CheckOut_CardioOnlyWithACardioCharge_Closes()
    {
        var attendance = Attendance.CheckInCardioOnly(MemberId, SubscriptionId, Guid.NewGuid(), CheckedInAt);

        var result = attendance.CheckOut(CheckedInAt.AddHours(1), hasCardioCharge: true);

        result.IsSuccess.ShouldBeTrue();
        attendance.CheckedOutAt.ShouldBe(CheckedInAt.AddHours(1));
    }

    [Fact]
    public void CheckOut_OrdinaryVisitWithoutACardioCharge_Closes() =>
        OpenAttendance().CheckOut(CheckedInAt.AddHours(1)).IsSuccess.ShouldBeTrue();

    [Fact]
    public void Cancel_CardioOnlyWithoutACardioCharge_Succeeds()
    {
        var attendance = Attendance.CheckInCardioOnly(MemberId, SubscriptionId, Guid.NewGuid(), CheckedInAt);

        attendance.Cancel(CheckedInAt.AddMinutes(5), CancelWindowMinutes).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void CheckInGuestOnReservePlace_ValidSlot_HoldsTheReservePlace()
    {
        var attendance = Attendance.CheckInGuestOnReservePlace("مریم احمدی", 3, CheckedInAt).Value;

        attendance.ReserveSlot.ShouldBe(3);
        attendance.LockerId.ShouldBeNull();
        attendance.IsGuest.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CheckInGuest_BlankName_FailsWithGuestNameRequired(string? name)
    {
        Attendance.CheckInGuest(name!, Guid.NewGuid(), CheckedInAt).Error.ShouldBe(AttendanceErrors.GuestNameRequired);
        Attendance.CheckInGuestOnReservePlace(name!, 1, CheckedInAt).Error.ShouldBe(AttendanceErrors.GuestNameRequired);
    }

    [Fact]
    public void CheckInGuest_NameAtTheLimitAfterTrimming_IsAccepted()
    {
        var name = new string('ن', Attendance.GuestNameMaxLength);

        Attendance.CheckInGuest($" {name} ", Guid.NewGuid(), CheckedInAt).Value.GuestName.ShouldBe(name);
    }

    [Fact]
    public void CheckInGuest_NameOverTheLimit_FailsWithGuestNameTooLong()
    {
        var name = new string('ن', Attendance.GuestNameMaxLength + 1);

        Attendance.CheckInGuest(name, Guid.NewGuid(), CheckedInAt).Error.ShouldBe(AttendanceErrors.GuestNameTooLong);
    }

    [Fact]
    public void CheckInGuestOnReservePlace_SlotOutOfRange_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => Attendance.CheckInGuestOnReservePlace("مریم احمدی", Attendance.ReservePlaceCount + 1, CheckedInAt));
    }

    [Fact]
    public void CheckOut_GuestWithUnpaidPurchase_FailsAndStaysOpen()
    {
        var guest = GuestAttendance();

        var result = guest.CheckOut(CheckedInAt.AddHours(1), hasUnpaidPurchases: true);

        result.Error.ShouldBe(AttendanceErrors.GuestHasUnpaidPurchases);
        guest.CheckedOutAt.ShouldBeNull();
    }

    [Fact]
    public void CheckOut_GuestWithEverythingPaid_Closes()
    {
        var guest = GuestAttendance();
        var at = CheckedInAt.AddHours(1);

        guest.CheckOut(at, hasUnpaidPurchases: false).IsSuccess.ShouldBeTrue();

        guest.CheckedOutAt.ShouldBe(at);
    }

    [Fact]
    public void CheckOut_MemberWithUnpaidPurchase_StillCloses()
    {
        // A member's debt stays on their account and is never enforced (BUSINESS_RULES.md §5).
        var attendance = OpenAttendance();

        attendance.CheckOut(CheckedInAt.AddHours(1), hasUnpaidPurchases: true).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Cancel_GuestLeavingAnUnpaidPurchase_FailsAndStaysOpen()
    {
        var guest = GuestAttendance();

        var result = guest.Cancel(CheckedInAt.AddMinutes(5), CancelWindowMinutes, leavesUnpaidPurchases: true);

        result.Error.ShouldBe(AttendanceErrors.GuestHasUnpaidPurchases);
        guest.CheckedOutAt.ShouldBeNull();
        guest.CancelledAt.ShouldBeNull();
    }

    [Fact]
    public void Cancel_GuestWithNothingUnpaidLeft_Cancels()
    {
        var guest = GuestAttendance();

        guest.Cancel(CheckedInAt.AddMinutes(5), CancelWindowMinutes, leavesUnpaidPurchases: false).IsSuccess.ShouldBeTrue();

        guest.CancelledAt.ShouldNotBeNull();
    }

    [Fact]
    public void AutoClose_GuestWithUnpaidCafe_StillCloses()
    {
        // The locker must be free the next morning (BUSINESS_RULES.md §7 Guest visit).
        var guest = GuestAttendance();
        var closedAt = CheckedInAt.AddHours(14);

        guest.AutoClose(closedAt);

        guest.AutoClosedAt.ShouldBe(closedAt);
    }

    private static Attendance OpenAttendance() =>
        Attendance.CheckIn(MemberId, SubscriptionId, Guid.NewGuid(), CheckedInAt);

    private static Attendance GuestAttendance() =>
        Attendance.CheckInGuest("مریم احمدی", Guid.NewGuid(), CheckedInAt).Value;
}
