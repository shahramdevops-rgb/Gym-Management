using Gym.Domain.Coaching;

namespace Gym.Domain.Tests.Coaching;

public sealed class TrainerRequestTests
{
    private static readonly Guid MemberId = Guid.CreateVersion7();
    private static readonly Guid TrainerId = Guid.CreateVersion7();

    /// <summary>The gym's today, named rather than read from a clock.</summary>
    private static readonly DateOnly Today = new(2026, 10, 29);

    [Fact]
    public void Create_FirstOfTheDay_IsPendingForThatTrainer()
    {
        var request = TrainerRequest.Create(MemberId, TrainerId, Today, madeToday: 0).Value;

        request.MemberId.ShouldBe(MemberId);
        request.TrainerId.ShouldBe(TrainerId);
        request.RequestedOn.ShouldBe(Today);
        request.Status.ShouldBe(TrainerRequestStatus.Pending);
        request.IsPending.ShouldBeTrue();
    }

    [Fact]
    public void Create_TheLastAllowedOfTheDay_Succeeds()
    {
        TrainerRequest.Create(MemberId, TrainerId, Today, TrainerRequest.MaxPerDay - 1).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_AfterTheDaysAllowance_FailsWithTooManyRequestsToday()
    {
        TrainerRequest.Create(MemberId, TrainerId, Today, TrainerRequest.MaxPerDay).Error.ShouldBe(CoachingErrors.TooManyRequestsToday);
    }

    [Fact]
    public void Accept_Pending_StartsALinkTodayFromThisRequest()
    {
        var request = Pending();

        var link = request.Accept(Today).Value;

        request.Status.ShouldBe(TrainerRequestStatus.Accepted);
        link.MemberId.ShouldBe(MemberId);
        link.TrainerId.ShouldBe(TrainerId);
        link.RequestId.ShouldBe(request.Id);
        link.StartedOn.ShouldBe(Today);
        link.IsOpen.ShouldBeTrue();
        link.EndReason.ShouldBeNull();
    }

    [Theory]
    [InlineData(TrainerRequestStatus.Declined)]
    [InlineData(TrainerRequestStatus.Cancelled)]
    [InlineData(TrainerRequestStatus.Replaced)]
    [InlineData(TrainerRequestStatus.TrainerStopped)]
    public void Accept_NoLongerPending_FailsAndStaysAsItWas(TrainerRequestStatus closedAs)
    {
        var request = Closed(closedAs);

        request.Accept(Today).Error.ShouldBe(CoachingErrors.RequestNoLongerPending);
        request.Status.ShouldBe(closedAs);
    }

    [Fact]
    public void Accept_Twice_FailsTheSecondTime()
    {
        var request = Pending();
        request.Accept(Today).IsSuccess.ShouldBeTrue();

        request.Accept(Today).Error.ShouldBe(CoachingErrors.RequestNoLongerPending);
    }

    [Fact]
    public void Decline_Pending_IsDeclined()
    {
        var request = Pending();

        request.Decline().IsSuccess.ShouldBeTrue();

        request.Status.ShouldBe(TrainerRequestStatus.Declined);
        request.IsPending.ShouldBeFalse();
    }

    [Fact]
    public void Cancel_Pending_IsCancelled()
    {
        var request = Pending();

        request.Cancel().IsSuccess.ShouldBeTrue();

        request.Status.ShouldBe(TrainerRequestStatus.Cancelled);
    }

    [Fact]
    public void Replace_Pending_IsReplaced()
    {
        var request = Pending();

        request.Replace().IsSuccess.ShouldBeTrue();

        request.Status.ShouldBe(TrainerRequestStatus.Replaced);
    }

    [Fact]
    public void EndForStoppedTrainer_Pending_IsTrainerStopped()
    {
        var request = Pending();

        request.EndForStoppedTrainer().IsSuccess.ShouldBeTrue();

        request.Status.ShouldBe(TrainerRequestStatus.TrainerStopped);
    }

    [Fact]
    public void Cancel_Accepted_FailsWithRequestNoLongerPending()
    {
        var request = Pending();
        request.Accept(Today).IsSuccess.ShouldBeTrue();

        request.Cancel().Error.ShouldBe(CoachingErrors.RequestNoLongerPending);
        request.Status.ShouldBe(TrainerRequestStatus.Accepted);
    }

    private static TrainerRequest Pending() => TrainerRequest.Create(MemberId, TrainerId, Today, madeToday: 0).Value;

    private static TrainerRequest Closed(TrainerRequestStatus status)
    {
        var request = Pending();
        var closed = status switch
        {
            TrainerRequestStatus.Declined => request.Decline(),
            TrainerRequestStatus.Cancelled => request.Cancel(),
            TrainerRequestStatus.Replaced => request.Replace(),
            TrainerRequestStatus.TrainerStopped => request.EndForStoppedTrainer(),
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };
        closed.IsSuccess.ShouldBeTrue();

        return request;
    }
}
