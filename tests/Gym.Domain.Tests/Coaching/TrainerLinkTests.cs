using Gym.Domain.Coaching;

namespace Gym.Domain.Tests.Coaching;

public sealed class TrainerLinkTests
{
    /// <summary>The gym's day the trainer accepted, named rather than read from a clock.</summary>
    private static readonly DateOnly StartedOn = new(2026, 10, 20);

    [Fact]
    public void End_Open_EndsTodayWithTheReason()
    {
        var link = Open();
        var today = StartedOn.AddDays(9);

        link.End(today, TrainerLinkEndReason.ByMember).IsSuccess.ShouldBeTrue();

        link.IsOpen.ShouldBeFalse();
        link.EndedOn.ShouldBe(today);
        link.EndReason.ShouldBe(TrainerLinkEndReason.ByMember);
    }

    [Fact]
    public void End_TheDayItStarted_Ends()
    {
        // Accepted and ended the same day: the former trainer stops seeing the member that day.
        var link = Open();

        link.End(StartedOn, TrainerLinkEndReason.ByTrainer).IsSuccess.ShouldBeTrue();

        link.EndedOn.ShouldBe(StartedOn);
    }

    [Fact]
    public void End_ClockBeforeItsStart_EndsOnItsStart()
    {
        var link = Open();

        link.End(StartedOn.AddDays(-1), TrainerLinkEndReason.TrainerStopped).IsSuccess.ShouldBeTrue();

        link.EndedOn.ShouldBe(StartedOn);
    }

    [Fact]
    public void End_AlreadyEnded_FailsAndKeepsTheFirstEnd()
    {
        var link = Open();
        var firstEnd = StartedOn.AddDays(2);
        link.End(firstEnd, TrainerLinkEndReason.NewTrainer).IsSuccess.ShouldBeTrue();

        link.End(StartedOn.AddDays(5), TrainerLinkEndReason.ByMember).Error.ShouldBe(CoachingErrors.LinkAlreadyEnded);

        link.EndedOn.ShouldBe(firstEnd);
        link.EndReason.ShouldBe(TrainerLinkEndReason.NewTrainer);
    }

    private static TrainerLink Open() =>
        TrainerRequest.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), StartedOn, madeToday: 0).Value.Accept(StartedOn).Value;
}
