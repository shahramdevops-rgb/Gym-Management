using Gym.Domain.Bookings;

using static Gym.Domain.Tests.Bookings.BookingRulesTests;

namespace Gym.Domain.Tests.Bookings;

/// <summary>A student's session the trainer cancelled: when its SMS is still due, and «دیدم» (task 15.6c).</summary>
public sealed class SessionCancellationTests
{
    private const int DaysAhead = 7;

    private static readonly TimeOnly Morning = new(9, 0);

    [Fact]
    public void Create_KeepsTheSessionAndTheSave()
    {
        var memberId = Guid.CreateVersion7();
        var trainerId = Guid.CreateVersion7();
        var changeId = Guid.CreateVersion7();

        var row = SessionCancellation.Create(memberId, trainerId, changeId, Today, new TimeOnly(13, 0), new TimeOnly(14, 0));

        row.MemberId.ShouldBe(memberId);
        row.TrainerId.ShouldBe(trainerId);
        row.ChangeId.ShouldBe(changeId);
        row.Date.ShouldBe(Today);
        row.Start.ShouldBe(new TimeOnly(13, 0));
        row.End.ShouldBe(new TimeOnly(14, 0));
        row.SeenAt.ShouldBeNull();
    }

    [Fact]
    public void Create_EmptyChangeId_Throws() =>
        Should.Throw<ArgumentException>(() =>
            SessionCancellation.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.Empty, Today, new TimeOnly(13, 0), new TimeOnly(14, 0)));

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(DaysAhead)]
    public void IsSmsDue_FromTodayToSevenDaysOn_IsTrue(int days)
    {
        Cancelled(Today.AddDays(days), new TimeOnly(13, 0)).IsSmsDue(Today, Morning, DaysAhead).ShouldBeTrue();
    }

    [Fact]
    public void IsSmsDue_EightDaysOn_IsFalse()
    {
        Cancelled(Today.AddDays(DaysAhead + 1), new TimeOnly(13, 0)).IsSmsDue(Today, Morning, DaysAhead).ShouldBeFalse();
    }

    [Fact]
    public void IsSmsDue_TodaysSessionStarted_IsFalse()
    {
        Cancelled(Today, new TimeOnly(8, 0)).IsSmsDue(Today, Morning, DaysAhead).ShouldBeFalse();
    }

    [Fact]
    public void IsSmsDue_SeenInTheApp_IsFalse()
    {
        var row = Cancelled(Today.AddDays(1), new TimeOnly(13, 0));

        row.MarkSeen(DateTimeOffset.UnixEpoch);

        row.IsSmsDue(Today, Morning, DaysAhead).ShouldBeFalse();
    }

    [Fact]
    public void MarkSeen_Twice_KeepsTheFirstMoment()
    {
        var row = Cancelled(Today.AddDays(1), new TimeOnly(13, 0));
        var first = new DateTimeOffset(2026, 10, 31, 8, 0, 0, TimeSpan.Zero);

        row.MarkSeen(first);
        row.MarkSeen(first.AddHours(1));

        row.SeenAt.ShouldBe(first);
    }

    private static SessionCancellation Cancelled(DateOnly date, TimeOnly start) =>
        SessionCancellation.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), date, start, start.AddHours(1));
}
