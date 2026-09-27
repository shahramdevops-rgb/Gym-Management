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

    private static Attendance OpenAttendance() =>
        Attendance.CheckIn(MemberId, SubscriptionId, Guid.NewGuid(), CheckedInAt);
}
