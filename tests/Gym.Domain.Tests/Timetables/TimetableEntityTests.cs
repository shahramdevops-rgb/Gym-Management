using Gym.Domain.Timetables;

using static Gym.Domain.Tests.Timetables.TimetableRulesTests;

namespace Gym.Domain.Tests.Timetables;

/// <summary>The trainer's two kinds of saved change: a weekly pattern from a date, and one date on its own.</summary>
public sealed class TimetableEntityTests
{
    private static readonly Guid TrainerId = Guid.CreateVersion7();

    private static readonly DateOnly Today = new(2026, 10, 30);

    private static readonly TimeOnly Noon = new(12, 0);

    // ---- The weekly pattern ----

    [Fact]
    public void CreatePattern_FromTomorrow_HoldsItsSessions()
    {
        var pattern = WeekPattern.Create(TrainerId, Today.AddDays(1), [Weekly(DayOfWeek.Saturday, 13), Weekly(DayOfWeek.Monday, 9)], Today).Value;

        pattern.TrainerId.ShouldBe(TrainerId);
        pattern.StartsOn.ShouldBe(Today.AddDays(1));
        pattern.Sessions.Select(session => session.ToWeekly()).ShouldBe([Weekly(DayOfWeek.Saturday, 13), Weekly(DayOfWeek.Monday, 9)]);
        pattern.Sessions.ShouldAllBe(session => session.PatternId == pattern.Id);
    }

    [Fact]
    public void CreatePattern_WithNoSessions_IsAWeekWithNone()
    {
        WeekPattern.Create(TrainerId, Today.AddDays(1), [], Today).Value.Sessions.ShouldBeEmpty();
    }

    [Fact]
    public void CreatePattern_OnTheLastDay_Succeeds()
    {
        WeekPattern.Create(TrainerId, TimetableRules.LastDay(Today), [], Today).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(TimetableRules.DaysAhead)]
    public void CreatePattern_TodayEarlierOrPastTheLastDay_FailsWithPatternStartOutOfRange(int daysFromToday)
    {
        WeekPattern.Create(TrainerId, Today.AddDays(daysFromToday), [], Today).Error.ShouldBe(TimetableErrors.PatternStartOutOfRange);
    }

    [Fact]
    public void CreatePattern_ABadSession_FailsWithItsError()
    {
        var session = new WeeklySession(DayOfWeek.Saturday, new TimeOnly(13, 0), new TimeOnly(14, 0), 0);

        WeekPattern.Create(TrainerId, Today.AddDays(1), [session], Today).Error.ShouldBe(TimetableErrors.CapacityOutOfRange);
    }

    // ---- One date on its own ----

    [Fact]
    public void CreateDay_ClosingIt_HasNoSessions()
    {
        var day = TimetableDay.Create(TrainerId, Today.AddDays(3), [Session(13, 0, 14, 0)], [], Today, Noon).Value;

        day.TrainerId.ShouldBe(TrainerId);
        day.Date.ShouldBe(Today.AddDays(3));
        day.IsClosed.ShouldBeTrue();
    }

    [Fact]
    public void CreateDay_RemovingAStartedSessionToday_FailsWithSessionStarted()
    {
        TimetableDay.Create(TrainerId, Today, [Session(11, 0, 13, 0)], [], Today, Noon).Error.ShouldBe(TimetableErrors.SessionStarted);
    }

    [Fact]
    public void ChangeDay_AgainWithNewSessions_ReplacesTheOldOnes()
    {
        var day = TimetableDay.Create(TrainerId, Today.AddDays(3), [], [Session(13, 0, 14, 0)], Today, Noon).Value;

        day.Change([Session(15, 0, 16, 0, 8), Session(16, 0, 17, 0)], Today, Noon).IsSuccess.ShouldBeTrue();

        day.Sessions.Select(session => session.ToPlanned()).ShouldBe([Session(15, 0, 16, 0, 8), Session(16, 0, 17, 0)]);
        day.Sessions.ShouldAllBe(session => session.DayId == day.Id);
    }

    [Fact]
    public void ChangeDay_TodayAfterItsSessionStarted_ChecksAgainstItsOwnSessions()
    {
        // Changed yesterday for today: the 11:00 session is today's, whatever the pattern says.
        var day = TimetableDay.Create(TrainerId, Today, [], [Session(11, 0, 13, 0)], Today.AddDays(-1), Noon).Value;

        day.Change([], Today, Noon).Error.ShouldBe(TimetableErrors.SessionStarted);
        day.Sessions.Count.ShouldBe(1);
    }

    [Fact]
    public void CheckReset_TodayToAPatternWithoutTheStartedSession_FailsWithSessionStarted()
    {
        var day = TimetableDay.Create(TrainerId, Today, [], [Session(11, 0, 13, 0)], Today.AddDays(-1), Noon).Value;

        day.CheckReset([Session(15, 0, 16, 0)], Today, Noon).Error.ShouldBe(TimetableErrors.SessionStarted);
        day.CheckReset([Session(11, 0, 13, 0), Session(15, 0, 16, 0)], Today, Noon).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void CheckReset_APastDay_FailsWithDayOutOfRange()
    {
        var day = TimetableDay.Create(TrainerId, Today, [], [], Today, Noon).Value;

        day.CheckReset([], Today.AddDays(1), Noon).Error.ShouldBe(TimetableErrors.DayOutOfRange);
    }
}
