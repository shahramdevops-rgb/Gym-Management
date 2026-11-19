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
    [InlineData(12, 45)]
    [InlineData(12, 55)]
    [InlineData(13, 0)]
    [InlineData(13, 5)]
    public void Mark_CheckInFromAQuarterBeforeToFiveMinutesAfterTheStart_IsOnTime(int hour, int minute)
    {
        var mark = MarkToday(Evening, In(hour, minute));

        mark.State.ShouldBe(SessionPresence.OnTime);
        mark.CheckedInAt.ShouldBe(new TimeOnly(hour, minute));
        (mark.MinutesEarly, mark.MinutesLate).ShouldBe((null, null));
    }

    [Theory]
    [InlineData(12, 30)]
    [InlineData(12, 44)]
    public void Mark_CheckInFromHalfAnHourBeforeToJustBeforeTheQuarter_IsEarly(int hour, int minute)
    {
        var mark = MarkToday(Evening, In(hour, minute));

        mark.State.ShouldBe(SessionPresence.Early);
        mark.CheckedInAt.ShouldBe(new TimeOnly(hour, minute));
    }

    [Theory]
    [InlineData(13, 6)]
    [InlineData(13, 16)]
    [InlineData(14, 29)]
    public void Mark_CheckInAfterFiveMinutesBeforeTheEnd_IsLate(int hour, int minute)
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
        mark.Visit.ShouldBeNull();
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
    public void Mark_CheckInAtOrAfterTheEnd_IsCameAfterNotAbsent(int hour, int minute)
    {
        var mark = MarkToday(Evening, In(hour, minute));

        mark.State.ShouldBe(SessionPresence.CameAfter);
        mark.CheckedInAt.ShouldBe(new TimeOnly(hour, minute));
        (mark.MinutesEarly, mark.MinutesLate).ShouldBe((null, null));
    }

    [Fact]
    public void Mark_CheckInAfterTheEndWhileTheSessionRuns_IsNotPossibleSoNotYet()
    {
        // Before the end nothing can have checked in after it; the session is still "not yet".
        var mark = MarkToday(new TimeOnly(14, 0));

        mark.State.ShouldBe(SessionPresence.NotYet);
    }

    [Fact]
    public void Mark_EarlyCheckInStillOpenAtTheStart_IsEarly()
    {
        var mark = MarkToday(Evening, In(11, 0));

        mark.State.ShouldBe(SessionPresence.Early);
        mark.CheckedInAt.ShouldBe(new TimeOnly(11, 0));
    }

    [Fact]
    public void Mark_EarlyCheckInLeftAfterTheStart_IsEarly()
    {
        var mark = MarkToday(Evening, In(11, 0, left: new TimeOnly(13, 20)));

        mark.State.ShouldBe(SessionPresence.Early);
    }

    [Theory]
    [InlineData(12, 0)]
    [InlineData(13, 0)]
    public void Mark_EarlyCheckInLeftByTheStart_IsAbsent(int leftHour, int leftMinute)
    {
        var mark = MarkToday(Evening, In(11, 0, left: new TimeOnly(leftHour, leftMinute)));

        // Came and went before the session: not in it, so absent; the visit is «خارج از سانس».
        mark.State.ShouldBe(SessionPresence.Absent);
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
        (second.State, second.MinutesEarly).ShouldBe((SessionPresence.Early, 25));
    }

    [Fact]
    public void CountsFor_CheckInJustBeforeTheHalfHour_DoesNotCountWhenLeftBeforeTheStart()
    {
        PresenceRules.CountsFor(In(12, 29, left: new TimeOnly(12, 50)), Start, End).ShouldBeFalse();
        PresenceRules.CountsFor(In(12, 30, left: new TimeOnly(12, 50)), Start, End).ShouldBeTrue();
    }

    // ---- 15.7d, 15.7m: «زودتر از موعد», the check-out, the visits outside the sessions ----

    [Theory]
    [InlineData(12, 30, 30)]
    [InlineData(12, 44, 16)]
    [InlineData(11, 0, 120)] // came long before and was still inside at the start
    public void Mark_CheckInBeforeTheQuarter_IsEarlyWithTheMinutesFromTheStart(int hour, int minute, int early)
    {
        var mark = MarkToday(Evening, In(hour, minute));

        (mark.State, mark.MinutesEarly, mark.MinutesLate).ShouldBe((SessionPresence.Early, early, null));
    }

    [Theory]
    [InlineData(12, 45)]
    [InlineData(12, 59)]
    [InlineData(13, 0)]
    [InlineData(13, 5)]
    public void Mark_OnTime_HasNoMinutes(int hour, int minute)
    {
        var mark = MarkToday(Evening, In(hour, minute));

        (mark.State, mark.MinutesEarly, mark.MinutesLate).ShouldBe((SessionPresence.OnTime, null, null));
    }

    [Fact]
    public void Mark_CheckInSecondsAfterFiveMinutes_IsStillOnTime()
    {
        // Whole minutes, as the desk shows them: 13:05:40 reads 13:05.
        var mark = MarkToday(Evening, new DayVisit(new TimeOnly(13, 5, 40), null));

        mark.State.ShouldBe(SessionPresence.OnTime);
    }

    [Theory]
    [InlineData(13, 6, 6)]
    [InlineData(13, 25, 25)]
    [InlineData(14, 29, 89)]
    public void Mark_Late_CountsTheMinutesFromTheStart(int hour, int minute, int late)
    {
        var mark = MarkToday(Evening, In(hour, minute));

        (mark.State, mark.MinutesEarly, mark.MinutesLate).ShouldBe((SessionPresence.Late, null, late));
    }

    [Fact]
    public void Mark_AbsentOrNotYet_HasNoMinutes()
    {
        MarkToday(Evening).ShouldSatisfyAllConditions(
            mark => mark.MinutesEarly.ShouldBeNull(),
            mark => mark.MinutesLate.ShouldBeNull());
        MarkToday(new TimeOnly(13, 40)).MinutesEarly.ShouldBeNull();
    }

    [Fact]
    public void Mark_CountedCheckIn_CarriesItsVisitForTheCheckOut()
    {
        var visit = In(13, 5, left: new TimeOnly(14, 40)) with { CheckedOutAt = new TimeOnly(14, 40) };

        var mark = MarkToday(Evening, In(9, 0, left: new TimeOnly(10, 0)), visit);

        mark.Visit.ShouldBe(visit);
        mark.Visit!.CheckedOutAt.ShouldBe(new TimeOnly(14, 40));
    }

    [Fact]
    public void Mark_CameAfter_CarriesTheLaterVisit()
    {
        var later = In(16, 0) with { AutoClosed = true };

        var mark = MarkToday(Evening, later);

        mark.State.ShouldBe(SessionPresence.CameAfter);
        mark.Visit.ShouldBe(later);
    }

    [Theory]
    [InlineData(SessionPresence.Early, true)]
    [InlineData(SessionPresence.OnTime, true)]
    [InlineData(SessionPresence.Late, true)]
    [InlineData(SessionPresence.CameAfter, false)]
    [InlineData(SessionPresence.Absent, false)]
    [InlineData(SessionPresence.NotYet, false)]
    [InlineData(SessionPresence.Leave, false)]
    [InlineData(SessionPresence.Cancelled, false)]
    public void IsPresent_EarlyOnTimeOrLate_IsTheOnlyPresent(SessionPresence state, bool present)
    {
        state.IsPresent().ShouldBe(present);
    }

    [Fact]
    public void OutsideSessions_CheckInThatCountsForASession_IsLeftOut()
    {
        var outside = PresenceRules.OutsideSessions([In(12, 50), In(18, 0), In(9, 0, left: new TimeOnly(10, 0))], [(Start, End)]);

        outside.Select(visit => visit.CheckedInAt).ShouldBe([new TimeOnly(9, 0), new TimeOnly(18, 0)]);
    }

    [Fact]
    public void OutsideSessions_NoSessionThatDay_ListsEveryCheckInInOrder()
    {
        var outside = PresenceRules.OutsideSessions([In(17, 0), In(8, 0)], []);

        outside.Select(visit => visit.CheckedInAt).ShouldBe([new TimeOnly(8, 0), new TimeOnly(17, 0)]);
    }

    [Fact]
    public void OutsideSessions_CheckInAfterTheSessionEnded_IsListed()
    {
        // Shown beside the missed session as «بعد از سانس آمد» too: it was outside it.
        PresenceRules.OutsideSessions([In(14, 30)], [(Start, End)]).ShouldHaveSingleItem().CheckedInAt.ShouldBe(new TimeOnly(14, 30));
    }
}
