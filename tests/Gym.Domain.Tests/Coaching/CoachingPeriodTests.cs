using Gym.Domain.Bookings;
using Gym.Domain.Coaching;

using static Gym.Domain.Tests.Bookings.BookingRulesTests;

namespace Gym.Domain.Tests.Coaching;

/// <summary>The coaching period (BUSINESS_RULES.md §15 <i>The coaching period</i>, 15.7h).</summary>
public sealed class CoachingPeriodTests
{
    /// <summary>The gym's day the trainer accepted, named rather than read from a clock.</summary>
    private static readonly DateOnly AcceptedOn = new(2026, 11, 10);

    // ---- Accepting with a number of days ----

    [Fact]
    public void Accept_NoDaysTyped_FirstPeriodIsThirtyDaysFromToday()
    {
        var link = Pending().Accept(AcceptedOn).Value;

        var period = link.Periods.ShouldHaveSingleItem();
        period.LinkId.ShouldBe(link.Id);
        period.StartsOn.ShouldBe(AcceptedOn);
        period.EndsOn.ShouldBe(AcceptedOn.AddDays(29));
        period.Days.ShouldBe(CoachingPeriod.DefaultDays);
        link.PeriodEndsOn.ShouldBe(AcceptedOn.AddDays(29));
    }

    [Theory]
    [InlineData(CoachingPeriod.MinDays)]
    [InlineData(45)]
    [InlineData(CoachingPeriod.MaxDays)]
    public void Accept_DaysInRange_PeriodHasThoseDays(int days)
    {
        var link = Pending().Accept(AcceptedOn, days).Value;

        link.Periods.ShouldHaveSingleItem().Days.ShouldBe(days);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(CoachingPeriod.MinDays - 1)]
    [InlineData(CoachingPeriod.MaxDays + 1)]
    public void Accept_DaysOutOfRange_FailsAndTheRequestStillWaits(int days)
    {
        var request = Pending();

        request.Accept(AcceptedOn, days).Error.ShouldBe(CoachingErrors.PeriodDaysOutOfRange);

        request.IsPending.ShouldBeTrue();
        request.AnsweredOn.ShouldBeNull();
    }

    // ---- Renewing ----

    [Fact]
    public void Renew_BeforeTheEnd_StartsTheDayAfterTheLastDay()
    {
        var link = Pending().Accept(AcceptedOn).Value;

        var period = link.Renew(30, AcceptedOn.AddDays(20)).Value;

        period.StartsOn.ShouldBe(AcceptedOn.AddDays(30));
        period.EndsOn.ShouldBe(AcceptedOn.AddDays(59));
        link.Periods.Count.ShouldBe(2);
        link.PeriodEndsOn.ShouldBe(AcceptedOn.AddDays(59));
    }

    [Fact]
    public void Renew_OnTheLastDay_StartsTomorrow()
    {
        var link = Pending().Accept(AcceptedOn, 10).Value;

        link.Renew(7, AcceptedOn.AddDays(9)).Value.StartsOn.ShouldBe(AcceptedOn.AddDays(10));
    }

    [Fact]
    public void Renew_AfterTheEnd_StartsToday()
    {
        // The days between are gone: no session was placed in them.
        var link = Pending().Accept(AcceptedOn, 10).Value;
        var today = AcceptedOn.AddDays(25);

        var period = link.Renew(14, today).Value;

        period.StartsOn.ShouldBe(today);
        period.EndsOn.ShouldBe(today.AddDays(13));
    }

    [Theory]
    [InlineData(CoachingPeriod.MinDaysLeft)]
    [InlineData(CoachingPeriod.MinDays - 1)]
    [InlineData(CoachingPeriod.MaxDays + 1)]
    public void Renew_DaysOutOfRange_FailsAndAddsNothing(int days)
    {
        var link = Pending().Accept(AcceptedOn).Value;

        link.Renew(days, AcceptedOn.AddDays(3)).Error.ShouldBe(CoachingErrors.PeriodDaysOutOfRange);

        link.Periods.Count.ShouldBe(1);
    }

    [Fact]
    public void Renew_LinkEnded_FailsWithLinkAlreadyEnded()
    {
        var link = Pending().Accept(AcceptedOn).Value;
        link.End(AcceptedOn.AddDays(5), TrainerLinkEndReason.ByMember);

        link.Renew(30, AcceptedOn.AddDays(6)).Error.ShouldBe(CoachingErrors.LinkAlreadyEnded);
    }

    // ---- A link made before 15.7h ----

    [Fact]
    public void Renew_LinkWithoutPeriod_DaysLeftStartToday()
    {
        var link = WithoutPeriod();
        var today = AcceptedOn.AddDays(4);

        var period = link.Renew(12, today).Value;

        period.StartsOn.ShouldBe(today);
        period.EndsOn.ShouldBe(today.AddDays(11));
    }

    [Fact]
    public void Renew_LinkWithoutPeriod_OneDayLeftIsAllowed()
    {
        var link = WithoutPeriod();

        link.Renew(CoachingPeriod.MinDaysLeft, AcceptedOn).Value.EndsOn.ShouldBe(AcceptedOn);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(CoachingPeriod.MaxDays + 1)]
    public void Renew_LinkWithoutPeriodDaysOutOfRange_Fails(int days)
    {
        var link = WithoutPeriod();

        link.Renew(days, AcceptedOn).Error.ShouldBe(CoachingErrors.PeriodDaysOutOfRange);

        link.PeriodEndsOn.ShouldBeNull();
    }

    // ---- «ویرایش دوره»: the days left, earlier or later (15.7k) ----

    [Fact]
    public void SetDaysLeft_FewerInsideOnePeriod_EndsItOnTheNewLastDay()
    {
        var link = Pending().Accept(AcceptedOn).Value;
        var today = AcceptedOn.AddDays(5);

        link.SetDaysLeft(10, today).IsSuccess.ShouldBeTrue();

        var period = link.Periods.ShouldHaveSingleItem();
        (period.StartsOn, period.EndsOn).ShouldBe((AcceptedOn, today.AddDays(9)));
    }

    [Fact]
    public void SetDaysLeft_RenewedThreeTimesByMistake_RemovesTheLaterPeriods()
    {
        // The real case (1405/08/24): 30 days, then three renewals of 30 the trainer did not mean.
        var link = Pending().Accept(AcceptedOn).Value;
        var today = AcceptedOn.AddDays(2);
        link.Renew(30, today);
        link.Renew(30, today);
        link.Renew(30, today);

        link.SetDaysLeft(28, today).IsSuccess.ShouldBeTrue();

        var period = link.Periods.ShouldHaveSingleItem();
        (period.StartsOn, period.EndsOn).ShouldBe((AcceptedOn, AcceptedOn.AddDays(29)));
    }

    [Fact]
    public void SetDaysLeft_LastDayInsideARenewal_KeepsTheEarlierAndEndsThatOne()
    {
        var link = Pending().Accept(AcceptedOn).Value;
        link.Renew(30, AcceptedOn);
        link.Renew(30, AcceptedOn);

        link.SetDaysLeft(40, AcceptedOn).IsSuccess.ShouldBeTrue();

        var periods = link.Periods.OrderBy(period => period.StartsOn).ToList();
        periods.Count.ShouldBe(2);
        periods[0].EndsOn.ShouldBe(AcceptedOn.AddDays(29));
        (periods[1].StartsOn, periods[1].EndsOn).ShouldBe((AcceptedOn.AddDays(30), AcceptedOn.AddDays(39)));
    }

    [Fact]
    public void SetDaysLeft_OneDay_EndsToday()
    {
        var link = Pending().Accept(AcceptedOn).Value;
        var today = AcceptedOn.AddDays(3);

        link.SetDaysLeft(CoachingPeriod.MinDaysLeft, today).IsSuccess.ShouldBeTrue();

        link.PeriodEndsOn.ShouldBe(today);
    }

    [Fact]
    public void SetDaysLeft_More_TheLastPeriodRunsOn()
    {
        var link = Pending().Accept(AcceptedOn).Value;

        link.SetDaysLeft(50, AcceptedOn).IsSuccess.ShouldBeTrue();

        var period = link.Periods.ShouldHaveSingleItem();
        (period.StartsOn, period.EndsOn).ShouldBe((AcceptedOn, AcceptedOn.AddDays(49)));
    }

    [Fact]
    public void SetDaysLeft_MorePastThe84DaysOfThePeriod_TheRestIsANewPeriodRightAfter()
    {
        var link = Pending().Accept(AcceptedOn).Value;
        var today = AcceptedOn.AddDays(20);

        link.SetDaysLeft(CoachingPeriod.MaxDays, today).IsSuccess.ShouldBeTrue();

        var periods = link.Periods.OrderBy(period => period.StartsOn).ToList();
        periods.Count.ShouldBe(2);
        (periods[0].StartsOn, periods[0].EndsOn).ShouldBe((AcceptedOn, AcceptedOn.AddDays(CoachingPeriod.MaxDays - 1)));
        periods[1].StartsOn.ShouldBe(AcceptedOn.AddDays(CoachingPeriod.MaxDays));
        periods[1].EndsOn.ShouldBe(today.AddDays(CoachingPeriod.MaxDays - 1));
        periods.ShouldAllBe(period => period.Days <= CoachingPeriod.MaxDays);
    }

    [Fact]
    public void SetDaysLeft_SameDaysLeft_ChangesNothing()
    {
        var link = Pending().Accept(AcceptedOn).Value;

        link.SetDaysLeft(30, AcceptedOn).IsSuccess.ShouldBeTrue();

        link.Periods.ShouldHaveSingleItem().EndsOn.ShouldBe(AcceptedOn.AddDays(29));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(CoachingPeriod.MaxDays + 1)]
    public void SetDaysLeft_OutOfRange_FailsAndChangesNothing(int daysLeft)
    {
        var link = Pending().Accept(AcceptedOn).Value;

        link.SetDaysLeft(daysLeft, AcceptedOn).Error.ShouldBe(CoachingErrors.PeriodDaysOutOfRange);

        link.PeriodEndsOn.ShouldBe(AcceptedOn.AddDays(29));
    }

    [Fact]
    public void SetDaysLeft_PeriodOver_FailsWithPeriodNotRunning()
    {
        var link = Pending().Accept(AcceptedOn, 10).Value;

        link.SetDaysLeft(5, AcceptedOn.AddDays(10)).Error.ShouldBe(CoachingErrors.PeriodNotRunning);
    }

    [Fact]
    public void SetDaysLeft_OnTheLastDay_Allowed()
    {
        var link = Pending().Accept(AcceptedOn, 10).Value;

        link.SetDaysLeft(5, AcceptedOn.AddDays(9)).IsSuccess.ShouldBeTrue();

        link.PeriodEndsOn.ShouldBe(AcceptedOn.AddDays(13));
    }

    [Fact]
    public void SetDaysLeft_LinkWithoutPeriod_FailsWithPeriodNotRunning()
    {
        WithoutPeriod().SetDaysLeft(5, AcceptedOn).Error.ShouldBe(CoachingErrors.PeriodNotRunning);
    }

    [Fact]
    public void SetDaysLeft_LinkEnded_FailsWithLinkAlreadyEnded()
    {
        var link = Pending().Accept(AcceptedOn).Value;
        link.End(AcceptedOn.AddDays(5), TrainerLinkEndReason.ByTrainer);

        link.SetDaysLeft(5, AcceptedOn.AddDays(5)).Error.ShouldBe(CoachingErrors.LinkAlreadyEnded);
    }

    // ---- The fixed schedule inside the period ----

    [Fact]
    public void CreateSchedule_EndsOnThePeriodsLastDay_Succeeds()
    {
        var periodEndsOn = Today.AddDays(29);

        Schedule(Today, periodEndsOn, periodEndsOn).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void CreateSchedule_EndsAfterThePeriod_FailsWithScheduleAfterPeriod()
    {
        var periodEndsOn = Today.AddDays(29);

        Schedule(Today, periodEndsOn.AddDays(1), periodEndsOn).Error.ShouldBe(BookingErrors.ScheduleAfterPeriod);
    }

    [Fact]
    public void CreateSchedule_PeriodOver_FailsWithScheduleAfterPeriod()
    {
        Schedule(Today, Today.AddDays(7), Today.AddDays(-1)).Error.ShouldBe(BookingErrors.ScheduleAfterPeriod);
    }

    [Fact]
    public void CreateSchedule_LinkWithoutPeriod_KeepsTheTimetablesLimitOnly()
    {
        Schedule(Today, Today.AddDays(55), periodEndsOn: null).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void EndOnPeriodEnd_ScheduleRunsPast_EndsOnThePeriodsLastDay()
    {
        var schedule = Schedule(Today, Today.AddDays(55), periodEndsOn: null).Value;

        schedule.EndOnPeriodEnd(Today.AddDays(9));

        schedule.EndsOn.ShouldBe(Today.AddDays(9));
    }

    [Fact]
    public void EndOnPeriodEnd_ScheduleEndsBefore_KeepsItsEnd()
    {
        var schedule = Schedule(Today, Today.AddDays(5), periodEndsOn: null).Value;

        schedule.EndOnPeriodEnd(Today.AddDays(9));

        schedule.EndsOn.ShouldBe(Today.AddDays(5));
    }

    [Fact]
    public void EndOnPeriodEnd_ScheduleStartsAfter_Throws()
    {
        var schedule = Schedule(Today.AddDays(10), Today.AddDays(20), periodEndsOn: null).Value;

        schedule.StartsAfter(Today.AddDays(9)).ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => schedule.EndOnPeriodEnd(Today.AddDays(9)));
    }

    // ---- Helpers ----

    private static TrainerRequest Pending() =>
        TrainerRequest.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), AcceptedOn, madeToday: 0).Value;

    /// <summary>An open link as EF Core reads one made before 15.7h: no period row.</summary>
    private static TrainerLink WithoutPeriod() => (TrainerLink)Activator.CreateInstance(typeof(TrainerLink), nonPublic: true)!;

    private static Gym.Domain.Common.Result<FixedSchedule> Schedule(DateOnly startsOn, DateOnly endsOn, DateOnly? periodEndsOn) =>
        FixedSchedule.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), startsOn, endsOn, [Slot(DayOfWeek.Saturday, 13)], Today, periodEndsOn);
}
