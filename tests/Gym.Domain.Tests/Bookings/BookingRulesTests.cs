using Gym.Domain.Bookings;
using Gym.Domain.Timetables;

namespace Gym.Domain.Tests.Bookings;

/// <summary>Students in the trainer's sessions (BUSINESS_RULES.md §15 <i>Students in sessions</i>): the places, the room, and a change's warning.</summary>
public sealed class BookingRulesTests
{
    private static readonly Guid TrainerId = Guid.CreateVersion7();

    private static readonly Guid LinkId = Guid.CreateVersion7();

    private static readonly Guid MemberId = Guid.CreateVersion7();

    /// <summary>A Saturday.</summary>
    internal static readonly DateOnly Today = new(2026, 10, 31);

    private static readonly TimeOnly Noon = new(12, 0);

    private static readonly IReadOnlyDictionary<Guid, DateOnly> NoLinkEnded = new Dictionary<Guid, DateOnly>();

    // ---- The weekly sessions of a fixed schedule ----

    [Fact]
    public void CheckSlots_DifferentDaysAndTimes_Succeeds()
    {
        BookingRules.CheckSlots([Slot(DayOfWeek.Saturday, 13), Slot(DayOfWeek.Monday, 13), Slot(DayOfWeek.Saturday, 17)]).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void CheckSlots_None_FailsWithScheduleWithoutSessions()
    {
        BookingRules.CheckSlots([]).Error.ShouldBe(BookingErrors.ScheduleWithoutSessions);
    }

    [Fact]
    public void CheckSlots_TheSameTwice_FailsWithDuplicateSlot()
    {
        BookingRules.CheckSlots([Slot(DayOfWeek.Saturday, 13), Slot(DayOfWeek.Saturday, 13)]).Error.ShouldBe(BookingErrors.DuplicateSlot);
    }

    [Fact]
    public void CheckSlots_TheSameTimesOnTwoDays_Succeeds()
    {
        BookingRules.CheckSlots([Slot(DayOfWeek.Saturday, 13), Slot(DayOfWeek.Sunday, 13)]).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(7, 13, 0, 14, 0)]
    [InlineData(6, 13, 3, 14, 0)]
    [InlineData(6, 14, 0, 13, 0)]
    public void CheckSlots_ABadDayOrTime_FailsWithInvalidSlot(int day, int startHour, int startMinute, int endHour, int endMinute)
    {
        var slot = new WeeklySlot((DayOfWeek)day, new TimeOnly(startHour, startMinute), new TimeOnly(endHour, endMinute));

        BookingRules.CheckSlots([slot]).Error.ShouldBe(BookingErrors.InvalidSlot);
    }

    [Fact]
    public void CheckSlots_MoreThanTheWeekHolds_FailsWithTooManySlots()
    {
        var slots = Enum.GetValues<DayOfWeek>()
            .SelectMany(day => Enumerable.Range(0, TimetableRules.MaxSessionsPerDay + 1).Select(i => new WeeklySlot(day, new TimeOnly(1, 0).AddMinutes(i * 5), new TimeOnly(23, 0))))
            .ToList();

        BookingRules.CheckSlots(slots).Error.ShouldBe(BookingErrors.TooManySlots);
    }

    // ---- Started ----

    [Theory]
    [InlineData(-1, 23, false)]
    [InlineData(0, 12, false)]
    [InlineData(0, 13, true)]
    [InlineData(1, 0, true)]
    public void HasStarted_ComparesTheDateThenTheStart(int daysFromToday, int startHour, bool open)
    {
        BookingRules.HasStarted(Today.AddDays(daysFromToday), new TimeOnly(startHour, 0), Today, Noon).ShouldBe(!open);
    }

    // ---- The places ----

    [Fact]
    public void Places_AWeeklySession_IsEverySuchDayOfTheSchedule()
    {
        var schedule = Schedule(Today, Today.AddDays(20), Slot(DayOfWeek.Saturday, 13), Slot(DayOfWeek.Monday, 9));

        var places = BookingRules.Places(Today, Today.AddDays(30), [schedule], NoLinkEnded, [], []);

        places.Select(place => (place.Date, place.Start.Hour)).ShouldBe(
        [
            (Today, 13), (Today.AddDays(2), 9), (Today.AddDays(7), 13), (Today.AddDays(9), 9), (Today.AddDays(14), 13), (Today.AddDays(16), 9),
        ]);
        places.ShouldAllBe(place => place.ScheduleId == schedule.Id && !place.IsMoved && place.TrainerId == TrainerId && place.MemberId == MemberId);
    }

    [Fact]
    public void Places_OnlyTheDatesAskedFor()
    {
        var schedule = Schedule(Today, Today.AddDays(20), Slot(DayOfWeek.Saturday, 13));

        BookingRules.Places(Today.AddDays(1), Today.AddDays(7), [schedule], NoLinkEnded, [], [])
            .Select(place => place.Date).ShouldBe([Today.AddDays(7)]);
    }

    [Fact]
    public void Places_ADateSkipped_IsLeftOut()
    {
        var schedule = Schedule(Today, Today.AddDays(14), Slot(DayOfWeek.Saturday, 13));
        var skip = SessionSkip.Create(Place(schedule, Today.AddDays(7), 13), BookedBy.Member, Today, Noon).Value;

        BookingRules.Places(Today, Today.AddDays(14), [schedule], NoLinkEnded, [skip], [])
            .Select(place => place.Date).ShouldBe([Today, Today.AddDays(14)]);
    }

    [Fact]
    public void Places_ADateMovedTo_IsIn()
    {
        var booking = Booking(Today.AddDays(1), 17);

        var place = BookingRules.Places(Today, Today.AddDays(7), [], NoLinkEnded, [], [booking]).ShouldHaveSingleItem();

        place.IsMoved.ShouldBeTrue();
        place.BookingId.ShouldBe(booking.Id);
        place.Date.ShouldBe(Today.AddDays(1));
    }

    [Fact]
    public void Places_ALinkThatEnded_StopsAfterItsLastDay()
    {
        var schedule = Schedule(Today, Today.AddDays(14), Slot(DayOfWeek.Saturday, 13));
        var booking = Booking(Today.AddDays(8), 17);
        var ended = new Dictionary<Guid, DateOnly> { [LinkId] = Today.AddDays(7) };

        BookingRules.Places(Today, Today.AddDays(14), [schedule], ended, [], [booking])
            .Select(place => place.Date).ShouldBe([Today, Today.AddDays(7)]);
    }

    [Fact]
    public void Places_TheSameSessionTwice_IsOnePlace()
    {
        var schedule = Schedule(Today, Today, Slot(DayOfWeek.Saturday, 13));

        BookingRules.Places(Today, Today, [schedule], NoLinkEnded, [], [Booking(Today, 13)]).ShouldHaveSingleItem().ScheduleId.ShouldBe(schedule.Id);
    }

    // ---- The room ----

    [Fact]
    public void Fit_RoomOnEveryDate_HoldsThemAllWithNoneFull()
    {
        var schedule = Schedule(Today, Today.AddDays(13), Slot(DayOfWeek.Saturday, 13));
        var places = BookingRules.Places(Today, Today.AddDays(13), [schedule], NoLinkEnded, [], []);

        var fit = BookingRules.Fit(schedule, places.ToList(), _ => [Session(13, 2)]).Value;

        fit.Held.Count.ShouldBe(2);
        fit.Full.ShouldBeEmpty();
    }

    [Fact]
    public void Fit_ADateWithNoRoomForOneMore_IsFull()
    {
        var schedule = Schedule(Today, Today.AddDays(13), Slot(DayOfWeek.Saturday, 13));
        var other = OtherSchedule(Today.AddDays(7), Today.AddDays(7), Slot(DayOfWeek.Saturday, 13));
        var places = BookingRules.Places(Today, Today.AddDays(13), [schedule, other], NoLinkEnded, [], []);

        var fit = BookingRules.Fit(schedule, places.ToList(), _ => [Session(13, 1)]).Value;

        fit.Full.ShouldHaveSingleItem().Date.ShouldBe(Today.AddDays(7));
    }

    [Fact]
    public void Fit_ADateWithoutTheSession_IsPassedOver()
    {
        var schedule = Schedule(Today, Today.AddDays(13), Slot(DayOfWeek.Saturday, 13));
        var places = BookingRules.Places(Today, Today.AddDays(13), [schedule], NoLinkEnded, [], []);

        var fit = BookingRules.Fit(schedule, places.ToList(), date => date == Today ? [] : [Session(13, 5)]).Value;

        fit.Held.ShouldHaveSingleItem().Date.ShouldBe(Today.AddDays(7));
    }

    [Fact]
    public void Fit_AWeeklySessionOnNoneOfItsDates_FailsWithNoSuchSession()
    {
        var schedule = Schedule(Today, Today.AddDays(13), Slot(DayOfWeek.Saturday, 13), Slot(DayOfWeek.Saturday, 17));
        var places = BookingRules.Places(Today, Today.AddDays(13), [schedule], NoLinkEnded, [], []);

        BookingRules.Fit(schedule, places.ToList(), _ => [Session(13, 5)]).Error.ShouldBe(BookingErrors.NoSuchSession);
    }

    // ---- Moving a date ----

    [Fact]
    public void CheckMoveTarget_ASessionWithRoom_Succeeds()
    {
        BookingRules.CheckMoveTarget(MemberId, Today.AddDays(1), Session(13, 1), [], Today.AddDays(7), Today, Noon).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(8)]
    public void CheckMoveTarget_BeforeTodayOrAfterTheScheduleEnds_FailsWithMoveOutOfSchedule(int daysFromToday)
    {
        BookingRules.CheckMoveTarget(MemberId, Today.AddDays(daysFromToday), Session(13, 5), [], Today.AddDays(7), Today, Noon)
            .Error.ShouldBe(BookingErrors.MoveOutOfSchedule);
    }

    [Fact]
    public void CheckMoveTarget_NoSuchSession_FailsWithNoSuchSession()
    {
        BookingRules.CheckMoveTarget(MemberId, Today.AddDays(1), target: null, [], Today.AddDays(7), Today, Noon).Error.ShouldBe(BookingErrors.NoSuchSession);
    }

    [Fact]
    public void CheckMoveTarget_StartedToday_FailsWithSessionStarted()
    {
        BookingRules.CheckMoveTarget(MemberId, Today, Session(11, 5), [], Today.AddDays(7), Today, Noon).Error.ShouldBe(BookingErrors.SessionStarted);
    }

    [Fact]
    public void CheckMoveTarget_AlreadyInIt_FailsWithAlreadyInSession()
    {
        var places = BookingRules.Places(Today, Today, [Schedule(Today, Today, Slot(DayOfWeek.Saturday, 13))], NoLinkEnded, [], []);

        BookingRules.CheckMoveTarget(MemberId, Today, Session(13, 5), places.ToList(), Today.AddDays(7), Today, Noon).Error.ShouldBe(BookingErrors.AlreadyInSession);
    }

    [Fact]
    public void CheckMoveTarget_NoRoom_FailsWithSessionFull()
    {
        var other = OtherSchedule(Today, Today, Slot(DayOfWeek.Saturday, 13));
        var places = BookingRules.Places(Today, Today, [other], NoLinkEnded, [], []);

        BookingRules.CheckMoveTarget(MemberId, Today, Session(13, 1), places.ToList(), Today.AddDays(7), Today, Noon).Error.ShouldBe(BookingErrors.SessionFull);
    }

    // ---- A change of the timetable ----

    [Fact]
    public void Impact_ASessionWithStudentsRemoved_IsTouchedAsRemoved()
    {
        var places = BookingRules.Places(Today, Today.AddDays(7), [Schedule(Today, Today.AddDays(7), Slot(DayOfWeek.Saturday, 13))], NoLinkEnded, [], []);

        var touched = BookingRules.Impact(places.ToList(), _ => [Session(13, 5), Session(17, 5)], date => date == Today ? [Session(17, 5)] : [Session(13, 5)]);

        var session = touched.ShouldHaveSingleItem();
        session.Date.ShouldBe(Today);
        session.Removed.ShouldBeTrue();
        session.Students.ShouldBe([MemberId]);
    }

    [Fact]
    public void Impact_ACapacityBelowItsStudents_IsTouchedNotRemoved()
    {
        var places = BookingRules.Places(Today, Today, [
            Schedule(Today, Today, Slot(DayOfWeek.Saturday, 13)),
            OtherSchedule(Today, Today, Slot(DayOfWeek.Saturday, 13)),
        ], NoLinkEnded, [], []);

        var touched = BookingRules.Impact(places.ToList(), _ => [Session(13, 5)], _ => [Session(13, 1)]);

        var session = touched.ShouldHaveSingleItem();
        session.Removed.ShouldBeFalse();
        session.Students.Count.ShouldBe(2);
    }

    [Fact]
    public void Impact_RoomStillEnoughOrNoStudents_TouchesNothing()
    {
        var places = BookingRules.Places(Today, Today, [Schedule(Today, Today, Slot(DayOfWeek.Saturday, 13))], NoLinkEnded, [], []);

        BookingRules.Impact(places.ToList(), _ => [Session(13, 5), Session(17, 5)], _ => [Session(13, 1)]).ShouldBeEmpty();
    }

    // ---- Helpers ----

    internal static WeeklySlot Slot(DayOfWeek day, int hour) => new(day, new TimeOnly(hour, 0), new TimeOnly(hour + 1, 0));

    private static PlannedSession Session(int hour, int capacity) => new(new TimeOnly(hour, 0), new TimeOnly(hour + 1, 0), capacity);

    private static FixedSchedule Schedule(DateOnly startsOn, DateOnly endsOn, params WeeklySlot[] slots) =>
        FixedSchedule.Create(TrainerId, MemberId, LinkId, startsOn, endsOn, slots, Today).Value;

    /// <summary>Another student's, under a link of their own.</summary>
    private static FixedSchedule OtherSchedule(DateOnly startsOn, DateOnly endsOn, WeeklySlot slot) =>
        FixedSchedule.Create(TrainerId, Guid.CreateVersion7(), Guid.CreateVersion7(), startsOn, endsOn, [slot], Today).Value;

    private static BookedPlace Place(FixedSchedule schedule, DateOnly date, int hour) =>
        new(TrainerId, schedule.MemberId, date, new TimeOnly(hour, 0), new TimeOnly(hour + 1, 0), schedule.Id, BookingId: null);

    private static SessionBooking Booking(DateOnly date, int hour) =>
        SessionBooking.Create(TrainerId, MemberId, LinkId, date, new TimeOnly(hour, 0), new TimeOnly(hour + 1, 0), BookedBy.Member, Today, Noon).Value;
}
