using Gym.Domain.Coaching;

namespace Gym.Domain.Tests.Coaching;

public sealed class CoachingNewsTests
{
    /// <summary>The gym's today, named rather than read from a clock.</summary>
    private static readonly DateOnly Today = new(2026, 11, 10);

    [Fact]
    public void IsShown_Today_IsShown()
    {
        CoachingNews.IsShown(Today, Today).ShouldBeTrue();
    }

    [Fact]
    public void IsShown_TheLastOfTheThirtyDays_IsShown()
    {
        // Its own day is the first of the thirty: 29 days later is the thirtieth.
        CoachingNews.IsShown(Today.AddDays(-29), Today).ShouldBeTrue();
    }

    [Fact]
    public void IsShown_ThirtyDaysLater_IsNoLongerShown()
    {
        CoachingNews.IsShown(Today.AddDays(-30), Today).ShouldBeFalse();
    }

    [Fact]
    public void FirstShownDay_Today_IsTwentyNineDaysBefore()
    {
        CoachingNews.FirstShownDay(Today).ShouldBe(new DateOnly(2026, 10, 12));
    }

    [Theory]
    [InlineData(TrainerLinkEndReason.ByTrainer, true)]
    [InlineData(TrainerLinkEndReason.ByMember, false)]
    [InlineData(TrainerLinkEndReason.NewTrainer, false)]
    [InlineData(TrainerLinkEndReason.TrainerStopped, false)]
    public void TellsMember_EachReason_OnlyTheTrainersOwnEnd(TrainerLinkEndReason reason, bool told)
    {
        CoachingNews.TellsMember(reason).ShouldBe(told);
    }

    [Theory]
    [InlineData(TrainerLinkEndReason.ByMember, true)]
    [InlineData(TrainerLinkEndReason.ByTrainer, false)]
    [InlineData(TrainerLinkEndReason.NewTrainer, false)]
    [InlineData(TrainerLinkEndReason.TrainerStopped, false)]
    public void TellsTrainer_EachReason_OnlyTheStudentsOwnEnd(TrainerLinkEndReason reason, bool told)
    {
        CoachingNews.TellsTrainer(reason).ShouldBe(told);
    }
}
