using Gym.Domain.Coaching;

namespace Gym.Domain.Tests.Coaching;

/// <summary>«درخواست تمدید» (BUSINESS_RULES.md §15 <i>The coaching period</i>, task 15.7o).</summary>
public sealed class RenewalRequestTests
{
    /// <summary>The gym's day the trainer accepted, named rather than read from a clock.</summary>
    private static readonly DateOnly AcceptedOn = new(2026, 11, 10);

    /// <summary>The SMS settings' number for the period's last days, as it starts.</summary>
    private const int SoonDays = 7;

    /// <summary>The last day of a 30-day period from <see cref="AcceptedOn"/>.</summary>
    private static readonly DateOnly LastDay = AcceptedOn.AddDays(29);

    // ---- When it may be asked ----

    [Fact]
    public void Create_SoonDaysLeft_WaitsWithTheLastDay()
    {
        // Seven days left, the last day counting as one: the first day of the window.
        var link = Link();
        var today = LastDay.AddDays(-6);

        var request = RenewalRequest.Create(link, today, SoonDays).Value;

        request.LinkId.ShouldBe(link.Id);
        request.RequestedOn.ShouldBe(today);
        request.PeriodEndsOn.ShouldBe(LastDay);
        request.Status.ShouldBe(RenewalRequestStatus.Pending);
        request.IsPending.ShouldBeTrue();
        request.AnsweredOn.ShouldBeNull();
    }

    [Fact]
    public void Create_OneDayMoreThanSoonDaysLeft_FailsWithRenewalNotYet()
    {
        RenewalRequest.Create(Link(), LastDay.AddDays(-7), SoonDays).Error.ShouldBe(CoachingErrors.RenewalNotYet);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(40)]
    public void Create_OnTheLastDayOrAfterTheEnd_Waits(int daysAfterTheLastDay)
    {
        var request = RenewalRequest.Create(Link(), LastDay.AddDays(daysAfterTheLastDay), SoonDays);

        request.IsSuccess.ShouldBeTrue();
        request.Value.PeriodEndsOn.ShouldBe(LastDay);
    }

    [Fact]
    public void Create_TheSettingsNumber_MovesTheWindow()
    {
        var link = Link();

        RenewalRequest.Create(link, LastDay.AddDays(-13), soonDays: 14).IsSuccess.ShouldBeTrue();
        RenewalRequest.Create(link, LastDay.AddDays(-3), soonDays: 3).Error.ShouldBe(CoachingErrors.RenewalNotYet);
    }

    [Fact]
    public void Create_LinkWithoutPeriod_FailsWithRenewalNotYet()
    {
        var link = (TrainerLink)Activator.CreateInstance(typeof(TrainerLink), nonPublic: true)!;

        RenewalRequest.Create(link, AcceptedOn, SoonDays).Error.ShouldBe(CoachingErrors.RenewalNotYet);
    }

    [Fact]
    public void Create_EndedLink_FailsWithLinkAlreadyEnded()
    {
        var link = Link();
        link.End(LastDay, TrainerLinkEndReason.ByMember).IsSuccess.ShouldBeTrue();

        RenewalRequest.Create(link, LastDay, SoonDays).Error.ShouldBe(CoachingErrors.LinkAlreadyEnded);
    }

    [Fact]
    public void Create_AfterARenewal_AsksAboutTheNewLastDay()
    {
        var link = Link();
        link.Renew(10, LastDay).IsSuccess.ShouldBeTrue();

        // Eleven days left on the old last day: too early, until seven are left.
        RenewalRequest.Create(link, LastDay, SoonDays).Error.ShouldBe(CoachingErrors.RenewalNotYet);
        var request = RenewalRequest.Create(link, LastDay.AddDays(4), SoonDays).Value;

        request.PeriodEndsOn.ShouldBe(LastDay.AddDays(10));
    }

    [Theory]
    [InlineData(-6, true)]
    [InlineData(-7, false)]
    [InlineData(5, true)]
    public void CanAsk_DaysFromTheLastDay_IsTheWindow(int daysFromTheLastDay, bool expected)
    {
        RenewalRequest.CanAsk(LastDay, LastDay.AddDays(daysFromTheLastDay), SoonDays).ShouldBe(expected);
    }

    [Fact]
    public void CanAsk_NoPeriod_IsFalse()
    {
        RenewalRequest.CanAsk(null, AcceptedOn, SoonDays).ShouldBeFalse();
    }

    // ---- Answers ----

    [Fact]
    public void Renew_Waiting_IsRenewedWithTheDay()
    {
        var request = Waiting();

        request.Renew(LastDay).IsSuccess.ShouldBeTrue();

        request.Status.ShouldBe(RenewalRequestStatus.Renewed);
        request.AnsweredOn.ShouldBe(LastDay);
        request.IsPending.ShouldBeFalse();
    }

    [Fact]
    public void Decline_Waiting_IsDeclinedWithTheDay()
    {
        var request = Waiting();

        request.Decline(LastDay).IsSuccess.ShouldBeTrue();

        request.Status.ShouldBe(RenewalRequestStatus.Declined);
        request.AnsweredOn.ShouldBe(LastDay);
    }

    [Fact]
    public void Decline_ClockBeforeTheRequest_AnswersOnTheRequestDay()
    {
        var request = Waiting();

        request.Decline(request.RequestedOn.AddDays(-1)).IsSuccess.ShouldBeTrue();

        request.AnsweredOn.ShouldBe(request.RequestedOn);
    }

    [Fact]
    public void Cancel_Waiting_IsCancelledWithNoAnswerDay()
    {
        var request = Waiting();

        request.Cancel().IsSuccess.ShouldBeTrue();

        request.Status.ShouldBe(RenewalRequestStatus.Cancelled);
        request.AnsweredOn.ShouldBeNull();
    }

    [Fact]
    public void CloseForEndedLink_Waiting_IsClosedWithNoAnswerDay()
    {
        var request = Waiting();

        request.CloseForEndedLink().IsSuccess.ShouldBeTrue();

        request.Status.ShouldBe(RenewalRequestStatus.LinkEnded);
        request.AnsweredOn.ShouldBeNull();
    }

    [Fact]
    public void Answers_NoLongerWaiting_FailAndKeepTheFirstOutcome()
    {
        var request = Waiting();
        request.Cancel().IsSuccess.ShouldBeTrue();

        request.Renew(LastDay).Error.ShouldBe(CoachingErrors.RenewalNoLongerPending);
        request.Decline(LastDay).Error.ShouldBe(CoachingErrors.RenewalNoLongerPending);
        request.Cancel().Error.ShouldBe(CoachingErrors.RenewalNoLongerPending);
        request.CloseForEndedLink().Error.ShouldBe(CoachingErrors.RenewalNoLongerPending);

        request.Status.ShouldBe(RenewalRequestStatus.Cancelled);
        request.AnsweredOn.ShouldBeNull();
    }

    // ---- «درخواست تمدید رد شد» ----

    [Fact]
    public void ShowsDecline_DeclinedAndTheSameLastDay_IsTrue()
    {
        var request = Waiting();
        request.Decline(LastDay).IsSuccess.ShouldBeTrue();

        request.ShowsDecline(LastDay).ShouldBeTrue();
    }

    [Fact]
    public void ShowsDecline_TheLastDayMovedSince_IsFalse()
    {
        // A «تمدید» or «ویرایش» after the decline hides it.
        var request = Waiting();
        request.Decline(LastDay).IsSuccess.ShouldBeTrue();

        request.ShowsDecline(LastDay.AddDays(30)).ShouldBeFalse();
        request.ShowsDecline(LastDay.AddDays(-2)).ShouldBeFalse();
    }

    [Fact]
    public void ShowsDecline_NotDeclined_IsFalse()
    {
        var waiting = Waiting();
        var renewed = Waiting();
        renewed.Renew(LastDay).IsSuccess.ShouldBeTrue();

        waiting.ShowsDecline(LastDay).ShouldBeFalse();
        renewed.ShowsDecline(LastDay).ShouldBeFalse();
    }

    private static TrainerLink Link() =>
        TrainerRequest.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), AcceptedOn, madeToday: 0).Value.Accept(AcceptedOn).Value;

    private static RenewalRequest Waiting() => RenewalRequest.Create(Link(), LastDay.AddDays(-3), SoonDays).Value;
}
