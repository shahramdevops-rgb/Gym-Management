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
        attendance.CheckedInAt.ShouldBe(CheckedInAt);
        attendance.CheckedOutAt.ShouldBeNull();
    }

    [Fact]
    public void CheckIn_NoLockerAvailable_LeavesLockerIdNull()
    {
        var attendance = Attendance.CheckIn(MemberId, SubscriptionId, lockerId: null, CheckedInAt);

        attendance.LockerId.ShouldBeNull();
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
