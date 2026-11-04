using Gym.Domain.Bookings;

namespace Gym.Domain.Tests.Bookings;

/// <summary>Whether a student came to a session, from the desk's check-ins (task 15.7, BUSINESS_RULES.md §15 <i>Attendance for the trainer</i>).</summary>
public sealed class PresenceRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 4);
    private static readonly TimeOnly Start = new(13, 0);
    private static readonly TimeOnly End = new(14, 30);

    // Late in the evening: today's 13:00 session has ended.
    private static readonly TimeOnly Evening = new(20, 0);

    private static PresenceMark MarkToday(TimeOnly now, params DayVisit[] visits) =>
        PresenceRules.Mark(Today, Start, End, visits, Today, now);

    private static DayVisit In(int hour, int minute, TimeOnly? left = null) => new(new TimeOnly(hour, minute), left);

    [Theory]
    [InlineData(12, 30)]
    [InlineData(12, 55)]
    [InlineData(13, 0)]
    [InlineData(13, 15)]
    public void Mark_CheckInFromHalfAnHourBeforeToAQuarterAfterTheStart_IsOnTime(int hour, int minute)
    {
        var mark = MarkToday(Evening, In(hour, minute));

        mark.State.ShouldBe(SessionPresence.OnTime);
        mark.CheckedInAt.ShouldBe(new TimeOnly(hour, minute));
        mark.CameAfter.ShouldBeFalse();
    }

    [Theory]
    [InlineData(13, 16)]
    [InlineData(14, 29)]
    public void Mark_CheckInAfterTheQuarterBeforeTheEnd_IsLate(int hour, int minute)
    {
        var mark = MarkToday(Evening, In(hour, minute));

        mark.State.ShouldBe(SessionPresence.Late);
        mark.CheckedInAt.ShouldBe(new TimeOnly(hour, minute));
    }

    [Fact]
    public void Mark_NoCheckInAfterTheEnd_IsAbsent()
    {
        var mark = MarkToday(Evening);

        mark.State.ShouldBe(SessionPresence.Absent);
        mark.CheckedInAt.ShouldBeNull();
        mark.CameAfter.ShouldBeFalse();
    }

    [Fact]
    public void Mark_NoCheckInBeforeTheEnd_IsNotYet()
    {
        var mark = MarkToday(new TimeOnly(13, 40));

        mark.State.ShouldBe(SessionPresence.NotYet);
        mark.CheckedInAt.ShouldBeNull();
    }

    [Fact]
    public void Mark_SessionOfALaterDate_IsNotYet()
    {
        var mark = PresenceRules.Mark(Today.AddDays(1), Start, End, [], Today, Evening);

        mark.State.ShouldBe(SessionPresence.NotYet);
    }

    [Fact]
    public void Mark_NoCheckInOnAnEarlierDate_IsAbsent()
    {
        var mark = PresenceRules.Mark(Today.AddDays(-1), Start, End, [], Today, new TimeOnly(8, 0));

        mark.State.ShouldBe(SessionPresence.Absent);
    }

    [Theory]
    [InlineData(14, 30)]
    [InlineData(18, 0)]
    public void Mark_CheckInAtOrAfterTheEnd_IsAbsentAndCameAfter(int hour, int minute)
    {
        var mark = MarkToday(Evening, In(hour, minute));

        mark.State.ShouldBe(SessionPresence.Absent);
        mark.CameAfter.ShouldBeTrue();
        mark.CheckedInAt.ShouldBe(new TimeOnly(hour, minute));
    }

    [Fact]
    public void Mark_CheckInAfterTheEndWhileTheSessionRuns_IsNotPossibleSoNotYet()
    {
        // Before the end nothing can have checked in after it; the session is still "not yet".
        var mark = MarkToday(new TimeOnly(14, 0));

        mark.State.ShouldBe(SessionPresence.NotYet);
        mark.CameAfter.ShouldBeFalse();
    }

    [Fact]
    public void Mark_EarlyCheckInStillOpenAtTheStart_IsOnTime()
    {
        var mark = MarkToday(Evening, In(11, 0));

        mark.State.ShouldBe(SessionPresence.OnTime);
        mark.CheckedInAt.ShouldBe(new TimeOnly(11, 0));
    }

    [Fact]
    public void Mark_EarlyCheckInLeftAfterTheStart_IsOnTime()
    {
        var mark = MarkToday(Evening, In(11, 0, left: new TimeOnly(13, 20)));

        mark.State.ShouldBe(SessionPresence.OnTime);
    }

    [Theory]
    [InlineData(12, 0)]
    [InlineData(13, 0)]
    public void Mark_EarlyCheckInLeftByTheStart_IsAbsent(int leftHour, int leftMinute)
    {
        var mark = MarkToday(Evening, In(11, 0, left: new TimeOnly(leftHour, leftMinute)));

        mark.State.ShouldBe(SessionPresence.Absent);
        mark.CameAfter.ShouldBeFalse();
    }

    [Fact]
    public void Mark_TwoCheckIns_TheEarliestThatCountsDecides()
    {
        // Left early in the morning, came back late for the session.
        var mark = MarkToday(Evening, In(13, 40), In(9, 0, left: new TimeOnly(10, 0)), In(13, 20));

        mark.State.ShouldBe(SessionPresence.Late);
        mark.CheckedInAt.ShouldBe(new TimeOnly(13, 20));
    }

    [Fact]
    public void Mark_OneCheckIn_CountsForEachOverlappingSessionOfTheStudent()
    {
        DayVisit[] visits = [In(13, 50)];

        var first = PresenceRules.Mark(Today, Start, End, visits, Today, Evening);
        var second = PresenceRules.Mark(Today, new TimeOnly(14, 15), new TimeOnly(16, 0), visits, Today, Evening);

        first.State.ShouldBe(SessionPresence.Late);
        second.State.ShouldBe(SessionPresence.OnTime);
    }

    [Fact]
    public void CountsFor_CheckInJustBeforeTheHalfHour_DoesNotCountWhenLeftBeforeTheStart()
    {
        PresenceRules.CountsFor(In(12, 29, left: new TimeOnly(12, 50)), Start, End).ShouldBeFalse();
        PresenceRules.CountsFor(In(12, 30, left: new TimeOnly(12, 50)), Start, End).ShouldBeTrue();
    }
}
