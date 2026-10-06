using Gym.Domain.Notifications;

namespace Gym.Domain.Tests.Notifications;

/// <summary>BUSINESS_RULES.md §0 and §10 <i>The daily runs</i>: 3 tries, 1 minute then 5, never after 22:00.</summary>
public sealed class SmsRetryScheduleTests
{
    private static readonly SmsRetrySchedule Schedule = new(3, [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)]);
    private static readonly TimeOnly Morning = new(10, 0);

    [Fact]
    public void NextDelay_AfterTheFirstTry_IsOneMinute()
    {
        Schedule.NextDelay(1, Morning).ShouldBe(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void NextDelay_AfterTheSecondTry_IsFiveMinutes()
    {
        Schedule.NextDelay(2, Morning).ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void NextDelay_AfterTheLastTry_IsNone()
    {
        Schedule.NextDelay(3, Morning).ShouldBeNull();
    }

    [Fact]
    public void NextDelay_TryLandingAt2200_IsMade()
    {
        Schedule.NextDelay(2, new TimeOnly(21, 55)).ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void NextDelay_TryLandingAfter2200_IsNotMade()
    {
        Schedule.NextDelay(2, new TimeOnly(21, 55, 1)).ShouldBeNull();
    }

    [Fact]
    public void NextDelay_LateEveningWait_DoesNotWrapPastMidnight()
    {
        // 23:58 plus 5 minutes is 00:03 on a clock, which is not early in the day.
        Schedule.NextDelay(2, new TimeOnly(23, 58)).ShouldBeNull();
    }

    [Fact]
    public void NextDelay_OneTryOnly_IsNone()
    {
        new SmsRetrySchedule(1, []).NextDelay(1, Morning).ShouldBeNull();
    }

    [Fact]
    public void Create_WaitsNotOneFewerThanTries_Throws()
    {
        Should.Throw<ArgumentException>(() => new SmsRetrySchedule(3, [TimeSpan.FromMinutes(1)]));
    }

    [Fact]
    public void Create_NegativeWait_Throws()
    {
        Should.Throw<ArgumentException>(() => new SmsRetrySchedule(2, [TimeSpan.FromMinutes(-1)]));
    }

    [Fact]
    public void Create_NoTries_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SmsRetrySchedule(0, []));
    }
}
