using Gym.Domain.Attendances;

namespace Gym.Domain.Tests.Attendances;

public sealed class AttendanceTests
{
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
}
