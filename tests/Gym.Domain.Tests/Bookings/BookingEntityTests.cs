using Gym.Domain.Bookings;
using Gym.Domain.Common;
using Gym.Domain.Timetables;

using static Gym.Domain.Tests.Bookings.BookingRulesTests;

namespace Gym.Domain.Tests.Bookings;

/// <summary>A student's fixed schedule, a date taken out of it and taken back, and a date moved to or kept.</summary>
public sealed class BookingEntityTests
{
    private static readonly Guid TrainerId = Guid.CreateVersion7();

    private static readonly Guid MemberId = Guid.CreateVersion7();

    private static readonly Guid LinkId = Guid.CreateVersion7();

    private static readonly TimeOnly Noon = new(12, 0);

    // ---- The fixed schedule ----

    [Fact]
    public void CreateSchedule_FromTodayForEightWeeks_HoldsItsSlots()
    {
        var schedule = Create(Today, Today.AddDays(55), Slot(DayOfWeek.Saturday, 13), Slot(DayOfWeek.Monday, 13)).Value;

        schedule.TrainerId.ShouldBe(TrainerId);
        schedule.MemberId.ShouldBe(MemberId);
        schedule.LinkId.ShouldBe(LinkId);
        schedule.Slots.Select(slot => slot.ToWeekly()).ShouldBe([Slot(DayOfWeek.Saturday, 13), Slot(DayOfWeek.Monday, 13)]);
        schedule.Slots.ShouldAllBe(slot => slot.ScheduleId == schedule.Id);
        schedule.SlotsOn(DayOfWeek.Monday).ShouldHaveSingleItem().Start.ShouldBe(new TimeOnly(13, 0));
    }

    [Fact]
    public void CreateSchedule_OneDayOnTheTimetablesLastDay_Succeeds()
    {
        var lastDay = TimetableRules.LastDay(Today);

        Create(lastDay, lastDay, Slot(lastDay.DayOfWeek, 13)).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(-1, 5)]
    [InlineData(5, 4)]
    [InlineData(0, TimetableRules.DaysAhead)]
    public void CreateSchedule_DatesOutOfRange_FailsWithScheduleDatesOutOfRange(int startsIn, int endsIn)
    {
        Create(Today.AddDays(startsIn), Today.AddDays(endsIn), Slot(DayOfWeek.Saturday, 13)).Error.ShouldBe(BookingErrors.ScheduleDatesOutOfRange);
    }

    [Fact]
    public void CreateSchedule_NoSlots_FailsWithScheduleWithoutSessions()
    {
        Create(Today, Today.AddDays(7)).Error.ShouldBe(BookingErrors.ScheduleWithoutSessions);
    }

    [Fact]
    public void EndBefore_AScheduleWithEarlierDates_EndsYesterday()
    {
        var schedule = Create(Today.AddDays(-7), Today.AddDays(30), Slot(DayOfWeek.Saturday, 13), today: Today.AddDays(-7)).Value;

        schedule.StartedBefore(Today).ShouldBeTrue();
        schedule.EndBefore(Today);

        schedule.EndsOn.ShouldBe(Today.AddDays(-1));
    }

    [Fact]
    public void EndBefore_AScheduleThatEndedEarlier_IsLeftAsItWas()
    {
        var schedule = Create(Today.AddDays(-7), Today.AddDays(-3), Slot(DayOfWeek.Saturday, 13), today: Today.AddDays(-7)).Value;

        schedule.EndBefore(Today);

        schedule.EndsOn.ShouldBe(Today.AddDays(-3));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void EndBefore_AScheduleFromTodayOrLater_Throws(int startsIn)
    {
        var schedule = Create(Today.AddDays(startsIn), Today.AddDays(30), Slot(DayOfWeek.Saturday, 13)).Value;

        schedule.StartedBefore(Today).ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => schedule.EndBefore(Today));
    }

    // ---- A date taken out ----

    [Theory]
    [InlineData(SkipReason.Left, BookedBy.Member)]
    [InlineData(SkipReason.Moved, BookedBy.Member)]
    [InlineData(SkipReason.TakenOut, BookedBy.Trainer)]
    public void CreateSkip_BeforeTheSessionStarts_RecordsWhyAndWhoDidIt(SkipReason reason, BookedBy by)
    {
        var place = SchedulePlace(Today, 13);

        var skip = SessionSkip.Create(place, reason, Today, Noon).Value;

        skip.ScheduleId.ShouldBe(place.ScheduleId!.Value);
        skip.Date.ShouldBe(Today);
        skip.Reason.ShouldBe(reason);
        skip.By.ShouldBe(by);
    }

    [Fact]
    public void CreateSkip_ASessionThatHasStarted_FailsWithSessionStarted()
    {
        SessionSkip.Create(SchedulePlace(Today, 12), SkipReason.Left, Today, Noon).Error.ShouldBe(BookingErrors.SessionStarted);
    }

    [Fact]
    public void CreateSkip_ADateMovedTo_Throws()
    {
        Should.Throw<ArgumentException>(() => SessionSkip.Create(BookingPlace(Today, 13), SkipReason.Left, Today, Noon));
    }

    [Fact]
    public void CreateSkip_PassedOver_ThrowsForItIsPassOversOwn()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => SessionSkip.Create(SchedulePlace(Today, 13), SkipReason.PassedOver, Today, Noon));
    }

    [Fact]
    public void CheckReturn_ALeaveBeforeTheSessionStarts_Succeeds()
    {
        var skip = SessionSkip.Create(SchedulePlace(Today, 13), SkipReason.Left, Today, Noon).Value;

        skip.CheckReturn(Today, Noon).IsSuccess.ShouldBeTrue();
        skip.CheckReturn(Today, new TimeOnly(13, 0)).Error.ShouldBe(BookingErrors.SessionStarted);
    }

    [Theory]
    [InlineData(SkipReason.Moved)]
    [InlineData(SkipReason.TakenOut)]
    public void CheckReturn_NotALeave_FailsWithNotLeft(SkipReason reason)
    {
        var skip = SessionSkip.Create(SchedulePlace(Today, 13), reason, Today, Noon).Value;

        skip.CheckReturn(Today, Noon).Error.ShouldBe(BookingErrors.NotLeft);
    }

    [Fact]
    public void CheckReturn_ADatePassedOver_FailsWithNotLeft()
    {
        SessionSkip.PassOver(SchedulePlace(Today, 13)).CheckReturn(Today, Noon).Error.ShouldBe(BookingErrors.NotLeft);
    }

    [Fact]
    public void PassOver_ASessionThatHasStarted_IsTakenOutByTheTrainer()
    {
        var place = SchedulePlace(Today, 11);

        var skip = SessionSkip.PassOver(place);

        skip.ScheduleId.ShouldBe(place.ScheduleId!.Value);
        skip.Date.ShouldBe(Today);
        skip.By.ShouldBe(BookedBy.Trainer);
        skip.Reason.ShouldBe(SkipReason.PassedOver);
    }

    [Fact]
    public void PassOver_ADateMovedTo_Throws()
    {
        Should.Throw<ArgumentException>(() => SessionSkip.PassOver(BookingPlace(Today, 13)));
    }

    // ---- A date moved to ----

    [Fact]
    public void CreateBooking_ASessionNotStarted_Succeeds()
    {
        var booking = Booking(Today, 13).Value;

        booking.By.ShouldBe(BookedBy.Member);
        booking.CheckRemove(Today, Noon).IsSuccess.ShouldBeTrue();
        booking.CheckRemove(Today, new TimeOnly(13, 0)).Error.ShouldBe(BookingErrors.SessionStarted);
    }

    [Fact]
    public void CreateBooking_ASessionThatHasStarted_FailsWithSessionStarted()
    {
        Booking(Today, 11).Error.ShouldBe(BookingErrors.SessionStarted);
    }

    [Fact]
    public void KeepBooking_AStartedSessionOfTheSchedule_IsTheTrainersAndNotAMove()
    {
        var booking = SessionBooking.Keep(SchedulePlace(Today, 11), LinkId);

        (booking.TrainerId, booking.MemberId, booking.LinkId, booking.Date, booking.Start).ShouldBe((TrainerId, MemberId, LinkId, Today, new TimeOnly(11, 0)));
        booking.By.ShouldBe(BookedBy.Trainer);
        booking.CheckRemove(Today, Noon).Error.ShouldBe(BookingErrors.SessionStarted);
        new BookedPlace(TrainerId, MemberId, Today, booking.Start, booking.End, ScheduleId: null, booking.Id, booking.By).IsMoved.ShouldBeFalse();
        BookingPlace(Today, 13).IsMoved.ShouldBeTrue();
    }

    [Fact]
    public void KeepBooking_ADateMovedTo_Throws()
    {
        Should.Throw<ArgumentException>(() => SessionBooking.Keep(BookingPlace(Today, 11), LinkId));
    }

    // ---- Helpers ----

    private static Result<FixedSchedule> Create(DateOnly startsOn, DateOnly endsOn, params WeeklySlot[] slots) =>
        Create(startsOn, endsOn, slots, Today);

    private static Result<FixedSchedule> Create(DateOnly startsOn, DateOnly endsOn, WeeklySlot slot, DateOnly today) =>
        Create(startsOn, endsOn, [slot], today);

    private static Result<FixedSchedule> Create(DateOnly startsOn, DateOnly endsOn, WeeklySlot[] slots, DateOnly today) =>
        FixedSchedule.Create(TrainerId, MemberId, LinkId, startsOn, endsOn, slots, today);

    private static BookedPlace SchedulePlace(DateOnly date, int hour) =>
        new(TrainerId, MemberId, date, new TimeOnly(hour, 0), new TimeOnly(hour + 1, 0), Guid.CreateVersion7(), BookingId: null, By: null);

    private static BookedPlace BookingPlace(DateOnly date, int hour) =>
        new(TrainerId, MemberId, date, new TimeOnly(hour, 0), new TimeOnly(hour + 1, 0), ScheduleId: null, Guid.CreateVersion7(), BookedBy.Member);

    private static Result<SessionBooking> Booking(DateOnly date, int hour) =>
        SessionBooking.Create(TrainerId, MemberId, LinkId, date, new TimeOnly(hour, 0), new TimeOnly(hour + 1, 0), BookedBy.Member, Today, Noon);
}
