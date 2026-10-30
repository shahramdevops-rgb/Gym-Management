using Gym.Domain.Timetables;

namespace Gym.Domain.Tests.Timetables;

public sealed class TimetableRulesTests
{
    private static readonly Guid TrainerId = Guid.CreateVersion7();

    /// <summary>The gym's today, a Friday, named rather than read from a clock.</summary>
    private static readonly DateOnly Today = new(2026, 10, 30);

    private static readonly TimeOnly Noon = new(12, 0);

    // ---- One day's sessions ----

    [Fact]
    public void CheckDay_OverlappingSessions_Succeeds()
    {
        TimetableRules.CheckDay([Session(13, 0, 15, 0), Session(14, 45, 17, 0)]).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void CheckDay_NoSessions_Succeeds()
    {
        TimetableRules.CheckDay([]).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void CheckDay_CapacityOutOfRange_FailsWithCapacityOutOfRange(int capacity)
    {
        TimetableRules.CheckDay([Session(13, 0, 14, 0, capacity)]).Error.ShouldBe(TimetableErrors.CapacityOutOfRange);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    public void CheckDay_CapacityAtTheLimits_Succeeds(int capacity)
    {
        TimetableRules.CheckDay([Session(13, 0, 14, 0, capacity)]).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void CheckDay_TimeOffTheFiveMinuteStep_FailsWithTimeNotOnStep()
    {
        TimetableRules.CheckDay([Session(13, 2, 14, 0)]).Error.ShouldBe(TimetableErrors.TimeNotOnStep);
    }

    [Fact]
    public void CheckDay_TimeWithSeconds_FailsWithTimeNotOnStep()
    {
        var session = new PlannedSession(new TimeOnly(13, 0, 30), new TimeOnly(14, 0), 5);

        TimetableRules.CheckDay([session]).Error.ShouldBe(TimetableErrors.TimeNotOnStep);
    }

    [Theory]
    [InlineData(14, 0, 14, 0)]
    [InlineData(23, 0, 1, 0)]
    public void CheckDay_EndNotAfterStart_FailsWithEndNotAfterStart(int startHour, int startMinute, int endHour, int endMinute)
    {
        TimetableRules.CheckDay([Session(startHour, startMinute, endHour, endMinute)]).Error.ShouldBe(TimetableErrors.EndNotAfterStart);
    }

    [Fact]
    public void CheckDay_SameStartAndEndTwice_FailsWithDuplicateSession()
    {
        TimetableRules.CheckDay([Session(13, 0, 14, 0, 5), Session(13, 0, 14, 0, 8)]).Error.ShouldBe(TimetableErrors.DuplicateSession);
    }

    [Fact]
    public void CheckDay_MoreThanTheDaysLimit_FailsWithTooManySessions()
    {
        var sessions = Enumerable.Range(0, TimetableRules.MaxSessionsPerDay + 1)
            .Select(index => new PlannedSession(new TimeOnly(1, 0).AddMinutes(index * 5), new TimeOnly(23, 0), 5))
            .ToList();

        TimetableRules.CheckDay(sessions).Error.ShouldBe(TimetableErrors.TooManySessions);
    }

    // ---- A week's sessions ----

    [Fact]
    public void CheckWeek_SameTimesOnTwoDays_Succeeds()
    {
        TimetableRules.CheckWeek([Weekly(DayOfWeek.Saturday, 13), Weekly(DayOfWeek.Monday, 13)]).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void CheckWeek_SameTimesTwiceOnOneDay_FailsWithDuplicateSession()
    {
        TimetableRules.CheckWeek([Weekly(DayOfWeek.Saturday, 13), Weekly(DayOfWeek.Saturday, 13)]).Error.ShouldBe(TimetableErrors.DuplicateSession);
    }

    [Fact]
    public void CheckWeek_ADayOutsideTheWeek_FailsWithInvalidDay()
    {
        TimetableRules.CheckWeek([Weekly((DayOfWeek)7, 13)]).Error.ShouldBe(TimetableErrors.InvalidDay);
    }

    // ---- Which dates can change ----

    [Fact]
    public void CheckChange_YesterdayOrPastTheLastDay_FailsWithDayOutOfRange()
    {
        TimetableRules.CheckChange(Today.AddDays(-1), [], [], Today, Noon).Error.ShouldBe(TimetableErrors.DayOutOfRange);
        TimetableRules.CheckChange(TimetableRules.LastDay(Today).AddDays(1), [], [], Today, Noon).Error.ShouldBe(TimetableErrors.DayOutOfRange);
    }

    [Fact]
    public void CheckChange_TheLastDay_Succeeds()
    {
        TimetableRules.LastDay(Today).ShouldBe(Today.AddDays(83));
        TimetableRules.CheckChange(TimetableRules.LastDay(Today), [], [Session(13, 0, 14, 0)], Today, Noon).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void CheckChange_TodayKeepingTheStartedSessionAndAddingALaterOne_Succeeds()
    {
        var started = Session(11, 0, 13, 0);

        TimetableRules.CheckChange(Today, [started, Session(15, 0, 16, 0)], [started, Session(18, 0, 19, 0)], Today, Noon)
            .IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void CheckChange_TodayRemovingAStartedSession_FailsWithSessionStarted()
    {
        TimetableRules.CheckChange(Today, [Session(11, 0, 13, 0)], [], Today, Noon).Error.ShouldBe(TimetableErrors.SessionStarted);
    }

    [Fact]
    public void CheckChange_TodayChangingAStartedSessionsCapacity_FailsWithSessionStarted()
    {
        TimetableRules.CheckChange(Today, [Session(11, 0, 13, 0, 5)], [Session(11, 0, 13, 0, 9)], Today, Noon)
            .Error.ShouldBe(TimetableErrors.SessionStarted);
    }

    [Fact]
    public void CheckChange_TodayASessionStartingNow_CountsAsStarted()
    {
        TimetableRules.CheckChange(Today, [Session(12, 0, 13, 0)], [], Today, Noon).Error.ShouldBe(TimetableErrors.SessionStarted);
    }

    [Fact]
    public void CheckChange_TodayANewSessionInThePast_FailsWithSessionInThePast()
    {
        TimetableRules.CheckChange(Today, [], [Session(10, 0, 11, 0)], Today, Noon).Error.ShouldBe(TimetableErrors.SessionInThePast);
    }

    [Fact]
    public void CheckChange_TomorrowAnyChange_IgnoresTheTimeOfDay()
    {
        TimetableRules.CheckChange(Today.AddDays(1), [Session(8, 0, 9, 0)], [Session(6, 0, 7, 0)], Today, Noon).IsSuccess.ShouldBeTrue();
    }

    // ---- A date's sessions ----

    [Fact]
    public void SessionsOn_NoPattern_IsEmpty()
    {
        TimetableRules.SessionsOn(Today, [], changedDay: null).ShouldBeEmpty();
    }

    [Fact]
    public void SessionsOn_ThePatternInForce_GivesThatWeekdaysSessionsEarliestFirst()
    {
        var saturday = new DateOnly(2026, 10, 31);
        var pattern = Pattern(Today.AddDays(1), Weekly(DayOfWeek.Saturday, 17), Weekly(DayOfWeek.Saturday, 13), Weekly(DayOfWeek.Sunday, 9));

        TimetableRules.SessionsOn(saturday, [pattern], changedDay: null).ShouldBe([Session(13, 0, 14, 0), Session(17, 0, 18, 0)]);
        TimetableRules.SessionsOn(saturday.AddDays(7), [pattern], changedDay: null).ShouldBe([Session(13, 0, 14, 0), Session(17, 0, 18, 0)]);
    }

    [Fact]
    public void SessionsOn_BeforeThePatternStarts_IsEmpty()
    {
        var pattern = Pattern(Today.AddDays(8), Weekly(DayOfWeek.Saturday, 13));

        TimetableRules.SessionsOn(new DateOnly(2026, 10, 31), [pattern], changedDay: null).ShouldBeEmpty();
    }

    [Fact]
    public void SessionsOn_TwoPatterns_TheLaterOneFromItsStart()
    {
        var first = Pattern(Today.AddDays(1), Weekly(DayOfWeek.Saturday, 13));
        var second = Pattern(Today.AddDays(8), Weekly(DayOfWeek.Saturday, 16));

        TimetableRules.SessionsOn(new DateOnly(2026, 10, 31), [second, first], changedDay: null).ShouldBe([Session(13, 0, 14, 0)]);
        TimetableRules.SessionsOn(new DateOnly(2026, 11, 7), [second, first], changedDay: null).ShouldBe([Session(16, 0, 17, 0)]);
    }

    [Fact]
    public void SessionsOn_AChangedDay_WinsOverThePattern()
    {
        var saturday = new DateOnly(2026, 10, 31);
        var pattern = Pattern(Today.AddDays(1), Weekly(DayOfWeek.Saturday, 13));
        var changed = TimetableDay.Create(TrainerId, saturday, [Session(13, 0, 14, 0)], [Session(19, 0, 20, 0)], Today, Noon).Value;

        TimetableRules.SessionsOn(saturday, [pattern], changed).ShouldBe([Session(19, 0, 20, 0)]);
    }

    [Fact]
    public void SessionsOn_AChangeForAnotherDate_Throws()
    {
        var changed = TimetableDay.Create(TrainerId, Today.AddDays(2), [], [], Today, Noon).Value;

        Should.Throw<ArgumentException>(() => TimetableRules.SessionsOn(Today.AddDays(1), [], changed));
    }

    // ---- Helpers ----

    internal static PlannedSession Session(int startHour, int startMinute, int endHour, int endMinute, int capacity = 5) =>
        new(new TimeOnly(startHour, startMinute), new TimeOnly(endHour, endMinute), capacity);

    /// <summary>An hour from <paramref name="startHour"/>, for 5.</summary>
    internal static WeeklySession Weekly(DayOfWeek day, int startHour) =>
        new(day, new TimeOnly(startHour, 0), new TimeOnly(startHour + 1, 0), 5);

    private static WeekPattern Pattern(DateOnly startsOn, params WeeklySession[] sessions) =>
        WeekPattern.Create(TrainerId, startsOn, sessions, Today).Value;
}
