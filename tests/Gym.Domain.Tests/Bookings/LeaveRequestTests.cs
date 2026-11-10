using Gym.Domain.Bookings;

namespace Gym.Domain.Tests.Bookings;

/// <summary>Leave by request (BUSINESS_RULES.md §15 <i>Leave by request</i>, task 15.7i).</summary>
public sealed class LeaveRequestTests
{
    /// <summary>The gym's today, named rather than read from a clock.</summary>
    private static readonly DateOnly Today = new(2026, 11, 10);

    private static readonly DateOnly Tomorrow = Today.AddDays(1);
    private static readonly TimeOnly Start = new(13, 0);
    private static readonly TimeOnly End = new(14, 0);
    private static readonly TimeOnly Morning = new(9, 0);

    // ---- Asking ----

    [Fact]
    public void Create_ForTomorrow_WaitsForTheTrainer()
    {
        var request = LeaveRequest.Create(Place(Tomorrow), Guid.CreateVersion7(), Today).Value;

        request.Status.ShouldBe(LeaveRequestStatus.Pending);
        request.RequestedOn.ShouldBe(Today);
        (request.Date, request.Start, request.End).ShouldBe((Tomorrow, Start, End));
        request.IsWaiting(Today, Morning).ShouldBeTrue();
    }

    [Fact]
    public void Create_ForToday_FailsWithLeaveTooLate()
    {
        // By the day before (decided by the developer, 1405/08/19), even hours before the session.
        LeaveRequest.Create(Place(Today), Guid.CreateVersion7(), Today).Error.ShouldBe(BookingErrors.LeaveTooLate);
    }

    [Fact]
    public void Create_ForAPastDate_FailsWithLeaveTooLate()
    {
        LeaveRequest.Create(Place(Today.AddDays(-1)), Guid.CreateVersion7(), Today).Error.ShouldBe(BookingErrors.LeaveTooLate);
    }

    // ---- Not answered by the start ----

    [Fact]
    public void StateOn_PendingBeforeTheStart_IsPending()
    {
        var request = Pending();

        request.StateOn(Tomorrow, new TimeOnly(12, 59)).ShouldBe(LeaveRequestStatus.Pending);
    }

    [Fact]
    public void StateOn_PendingAtTheStart_IsDeclined()
    {
        // Not answered by the start is declined (decided by the developer, 1405/08/19).
        var request = Pending();

        request.StateOn(Tomorrow, Start).ShouldBe(LeaveRequestStatus.Declined);
        request.IsWaiting(Tomorrow, Start).ShouldBeFalse();
        request.Status.ShouldBe(LeaveRequestStatus.Pending);
    }

    // ---- The trainer's answer ----

    [Fact]
    public void Accept_Waiting_IsAccepted()
    {
        var request = Pending();

        request.Accept(Tomorrow, new TimeOnly(12, 0)).IsSuccess.ShouldBeTrue();

        request.Status.ShouldBe(LeaveRequestStatus.Accepted);
        request.StateOn(Tomorrow, new TimeOnly(15, 0)).ShouldBe(LeaveRequestStatus.Accepted);
    }

    [Fact]
    public void Decline_Waiting_IsDeclined()
    {
        var request = Pending();

        request.Decline(Today, Morning).IsSuccess.ShouldBeTrue();

        request.Status.ShouldBe(LeaveRequestStatus.Declined);
    }

    [Fact]
    public void Accept_AfterTheStart_FailsWithLeaveNotWaiting()
    {
        var request = Pending();

        request.Accept(Tomorrow, Start).Error.ShouldBe(BookingErrors.LeaveNotWaiting);

        request.Status.ShouldBe(LeaveRequestStatus.Pending);
    }

    [Fact]
    public void Accept_AlreadyDeclined_FailsWithLeaveNotWaiting()
    {
        var request = Pending();
        request.Decline(Today, Morning);

        request.Accept(Today, Morning).Error.ShouldBe(BookingErrors.LeaveNotWaiting);

        request.Status.ShouldBe(LeaveRequestStatus.Declined);
    }

    // ---- The student taking it back ----

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TakeBack_WaitingOrAccepted_IsCancelled(bool accepted)
    {
        var request = Pending();
        if (accepted)
        {
            request.Accept(Today, Morning);
        }

        request.TakeBack(Tomorrow, new TimeOnly(12, 0)).IsSuccess.ShouldBeTrue();

        request.Status.ShouldBe(LeaveRequestStatus.Cancelled);
    }

    [Fact]
    public void TakeBack_AfterTheStart_FailsWithSessionStarted()
    {
        var request = Pending();
        request.Accept(Today, Morning);

        request.TakeBack(Tomorrow, Start).Error.ShouldBe(BookingErrors.SessionStarted);

        request.Status.ShouldBe(LeaveRequestStatus.Accepted);
    }

    [Fact]
    public void TakeBack_Declined_FailsWithLeaveNotWaiting()
    {
        var request = Pending();
        request.Decline(Today, Morning);

        request.TakeBack(Today, Morning).Error.ShouldBe(BookingErrors.LeaveNotWaiting);
    }

    // ---- Attendance on a date with leave ----

    [Fact]
    public void MarkOnLeave_NoCheckIn_IsLeave()
    {
        var mark = PresenceRules.MarkOnLeave(Today, Start, End, [], Today, new TimeOnly(20, 0));

        (mark.State, mark.HadLeave).ShouldBe((SessionPresence.Leave, false));
    }

    [Fact]
    public void MarkOnLeave_CameOnTimeAnyway_IsOnTimeWithHadLeave()
    {
        var mark = PresenceRules.MarkOnLeave(Today, Start, End, [new DayVisit(new TimeOnly(12, 50), null)], Today, new TimeOnly(20, 0));

        (mark.State, mark.HadLeave, mark.MinutesEarly).ShouldBe((SessionPresence.OnTime, true, 10));
    }

    [Fact]
    public void MarkOnLeave_CameLateAnyway_IsLateWithHadLeave()
    {
        var mark = PresenceRules.MarkOnLeave(Today, Start, End, [new DayVisit(new TimeOnly(13, 30), null)], Today, new TimeOnly(20, 0));

        (mark.State, mark.HadLeave, mark.MinutesLate).ShouldBe((SessionPresence.Late, true, 30));
    }

    [Fact]
    public void MarkOnLeave_CameOnlyAfterTheSession_IsLeave()
    {
        var mark = PresenceRules.MarkOnLeave(Today, Start, End, [new DayVisit(new TimeOnly(18, 0), null)], Today, new TimeOnly(20, 0));

        (mark.State, mark.HadLeave).ShouldBe((SessionPresence.Leave, false));
    }

    [Fact]
    public void MarkOnLeave_SessionNotOverAndNoCheckIn_IsLeaveNotNotYet()
    {
        var mark = PresenceRules.MarkOnLeave(Today, Start, End, [], Today, new TimeOnly(13, 20));

        mark.State.ShouldBe(SessionPresence.Leave);
    }

    // ---- Helpers ----

    private static BookedPlace Place(DateOnly date) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), date, Start, End, Guid.CreateVersion7(), BookingId: null, By: null);

    private static LeaveRequest Pending() => LeaveRequest.Create(Place(Tomorrow), Guid.CreateVersion7(), Today).Value;
}
