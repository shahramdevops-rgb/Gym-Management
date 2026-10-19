using Gym.Domain.Notifications;

namespace Gym.Domain.Tests.Notifications;

/// <summary>The broadcast's own rules (BUSINESS_RULES.md §10 <i>The broadcast</i>, task 10.13).</summary>
public sealed class SmsBroadcastTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 8, 0, 0, TimeSpan.Zero);

    // ---- Creating ----

    [Fact]
    public void Create_Valid_IsSendingWithTheTrimmedText()
    {
        var broadcast = SmsBroadcast.Create("  باشگاه فردا تعطیل است.  ", BroadcastAudience.ActiveMembers).Value;

        broadcast.Text.ShouldBe("باشگاه فردا تعطیل است.");
        broadcast.Audience.ShouldBe(BroadcastAudience.ActiveMembers);
        broadcast.Status.ShouldBe(BroadcastStatus.Sending);
        broadcast.StopReason.ShouldBeNull();
        broadcast.EndedAt.ShouldBeNull();
    }

    [Fact]
    public void Create_BrowserLineBreaks_AreWrittenAsOneCharacter()
    {
        SmsBroadcast.Create("خط اول\r\nخط دوم\rخط سوم", BroadcastAudience.ActiveMembers).Value.Text
            .ShouldBe("خط اول\nخط دوم\nخط سوم");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\n")]
    public void Create_BlankText_IsRefused(string text)
    {
        SmsBroadcast.Create(text, BroadcastAudience.ActiveMembers).Error.ShouldBe(SmsBroadcastErrors.TextRequired);
    }

    [Fact]
    public void Create_TextLongerThanTheColumn_IsRefused()
    {
        SmsBroadcast.Create(new string('ب', SmsBroadcast.TextMaxLength + 1), BroadcastAudience.ActiveMembers)
            .Error.ShouldBe(SmsBroadcastErrors.TooLong);
    }

    [Fact]
    public void Create_UnknownAudience_IsRefused()
    {
        SmsBroadcast.Create("متن", (BroadcastAudience)99).Error.ShouldBe(SmsBroadcastErrors.UnknownAudience);
    }

    // ---- The 3 parts ----

    [Fact]
    public void CheckLength_FitsInThreePartsWithTheLongestName_IsNull()
    {
        // «علی عزیز، » is 10 characters and «\nلغو11» 6: 185 more make 201, three full parts.
        SmsBroadcast.CheckLength(new string('ب', 185), "علی").ShouldBeNull();
    }

    [Fact]
    public void CheckLength_OneCharacterOverThreeParts_IsTooLong()
    {
        SmsBroadcast.CheckLength(new string('ب', 186), "علی").ShouldBe(SmsBroadcastErrors.TooLong);
    }

    [Fact]
    public void CheckLength_ALongerFirstName_MakesTheSameTextTooLong()
    {
        var text = new string('ب', 180);

        SmsBroadcast.CheckLength(text, "علی").ShouldBeNull();
        SmsBroadcast.CheckLength(text, "عبدالرحمن").ShouldBe(SmsBroadcastErrors.TooLong);
    }

    [Fact]
    public void CheckLength_NoRecipients_MeasuresWithAnEmptyName()
    {
        SmsBroadcast.CheckLength(new string('ب', 188), string.Empty).ShouldBeNull();
    }

    // ---- Where it stands ----

    [Fact]
    public void Finish_Sending_IsFinishedWithItsMoment()
    {
        var broadcast = New();

        broadcast.Finish(Now).IsSuccess.ShouldBeTrue();

        broadcast.Status.ShouldBe(BroadcastStatus.Finished);
        broadcast.EndedAt.ShouldBe(Now);
    }

    [Fact]
    public void Stop_Sending_IsStoppedWithItsReason()
    {
        var broadcast = New();

        broadcast.Stop(BroadcastStopReason.CreditExhausted, Now).IsSuccess.ShouldBeTrue();

        broadcast.Status.ShouldBe(BroadcastStatus.Stopped);
        broadcast.StopReason.ShouldBe(BroadcastStopReason.CreditExhausted);
        broadcast.EndedAt.ShouldBe(Now);
    }

    [Fact]
    public void Finish_AlreadyFinished_IsRefused()
    {
        var broadcast = New();
        broadcast.Finish(Now);

        broadcast.Finish(Now).Error.ShouldBe(SmsBroadcastErrors.NotSending);
        broadcast.Stop(BroadcastStopReason.Error, Now).Error.ShouldBe(SmsBroadcastErrors.NotSending);
    }

    [Fact]
    public void Resume_Stopped_IsSendingAgainWithoutItsReason()
    {
        var broadcast = New();
        broadcast.Stop(BroadcastStopReason.OutsideSendingHours, Now);

        broadcast.Resume().IsSuccess.ShouldBeTrue();

        broadcast.Status.ShouldBe(BroadcastStatus.Sending);
        broadcast.StopReason.ShouldBeNull();
        broadcast.EndedAt.ShouldBeNull();
    }

    [Fact]
    public void Resume_SendingOrFinished_IsRefused()
    {
        var broadcast = New();
        broadcast.Resume().Error.ShouldBe(SmsBroadcastErrors.NotStopped);

        broadcast.Finish(Now);
        broadcast.Resume().Error.ShouldBe(SmsBroadcastErrors.NotStopped);
    }

    private static SmsBroadcast New() => SmsBroadcast.Create("متن", BroadcastAudience.ActiveMembers).Value;
}
