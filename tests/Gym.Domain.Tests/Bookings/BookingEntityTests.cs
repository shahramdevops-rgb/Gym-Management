using Gym.Domain.Bookings;
using Gym.Domain.Common;
using Gym.Domain.Timetables;

using static Gym.Domain.Tests.Bookings.BookingRulesTests;

namespace Gym.Domain.Tests.Bookings;

/// <summary>A student's fixed schedule, a date taken out of it, and a date moved to.</summary>
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
    public void EndAfter_AScheduleThatHasStarted_EndsToday()
    {
        var schedule = Create(Today.AddDays(-7), Today.AddDays(30), Slot(DayOfWeek.Saturday, 13), today: Today.AddDays(-7)).Value;

        schedule.HasStartedBy(Today).ShouldBeTrue();
        schedule.EndAfter(Today);

        schedule.EndsOn.ShouldBe(Today);
    }

    [Fact]
    public void EndAfter_AScheduleThatEndsBeforeToday_IsLeftAsItWas()
    {
        var schedule = Create(Today.AddDays(-7), Today.AddDays(-1), Slot(DayOfWeek.Saturday, 13), today: Today.AddDays(-7)).Value;

        schedule.EndAfter(Today);

        schedule.EndsOn.ShouldBe(Today.AddDays(-1));
    }

    [Fact]
    public void EndAfter_AScheduleNotStarted_Throws()
    {
        var schedule = Create(Today.AddDays(1), Today.AddDays(30), Slot(DayOfWeek.Saturday, 13)).Value;

        schedule.HasStartedBy(Today).ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => schedule.EndAfter(Today));
    }

    // ---- A date taken out ----

    [Fact]
    public void CreateSkip_BeforeTheSessionStarts_RecordsWhoDidIt()
    {
        var place = SchedulePlace(Today, 13);

        var skip = SessionSkip.Create(place, BookedBy.Trainer, Today, Noon).Value;

        skip.ScheduleId.ShouldBe(place.ScheduleId!.Value);
        skip.Date.ShouldBe(Today);
        skip.By.ShouldBe(BookedBy.Trainer);
    }

    [Fact]
    public void CreateSkip_ASessionThatHasStarted_FailsWithSessionStarted()
    {
        SessionSkip.Create(SchedulePlace(Today, 12), BookedBy.Member, Today, Noon).Error.ShouldBe(BookingErrors.SessionStarted);
    }

    [Fact]
    public void CreateSkip_ADateMovedTo_Throws()
    {
        var place = new BookedPlace(TrainerId, MemberId, Today, new TimeOnly(13, 0), new TimeOnly(14, 0), ScheduleId: null, Guid.CreateVersion7());

        Should.Throw<ArgumentException>(() => SessionSkip.Create(place, BookedBy.Member, Today, Noon));
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

    // ---- Helpers ----

    private static Result<FixedSchedule> Create(DateOnly startsOn, DateOnly endsOn, params WeeklySlot[] slots) =>
        Create(startsOn, endsOn, slots, Today);

    private static Result<FixedSchedule> Create(DateOnly startsOn, DateOnly endsOn, WeeklySlot slot, DateOnly today) =>
        Create(startsOn, endsOn, [slot], today);

    private static Result<FixedSchedule> Create(DateOnly startsOn, DateOnly endsOn, WeeklySlot[] slots, DateOnly today) =>
        FixedSchedule.Create(TrainerId, MemberId, LinkId, startsOn, endsOn, slots, today);

    private static BookedPlace SchedulePlace(DateOnly date, int hour) =>
        new(TrainerId, MemberId, date, new TimeOnly(hour, 0), new TimeOnly(hour + 1, 0), Guid.CreateVersion7(), BookingId: null);

    private static Result<SessionBooking> Booking(DateOnly date, int hour) =>
        SessionBooking.Create(TrainerId, MemberId, LinkId, date, new TimeOnly(hour, 0), new TimeOnly(hour + 1, 0), BookedBy.Member, Today, Noon);
}
