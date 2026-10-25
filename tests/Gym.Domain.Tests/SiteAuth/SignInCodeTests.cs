using Gym.Domain.SiteAuth;

namespace Gym.Domain.Tests.SiteAuth;

/// <summary>The one-time code's own rules (BUSINESS_RULES.md §15 <i>The one-time code</i>).</summary>
public sealed class SignInCodeTests
{
    private const string Phone = "+989121234567";
    private const string Code = "04217";

    private static readonly DateTimeOffset Now = new(2026, 10, 26, 8, 0, 0, TimeSpan.Zero);

    // ---- Issue ----

    [Fact]
    public void Issue_WhenCalled_IsValidForSixtyMinutes()
    {
        var code = SignInCode.Issue(Phone, "seed-1", Now);

        code.PhoneNumber.ShouldBe(Phone);
        code.CodeSeed.ShouldBe("seed-1");
        code.ExpiresAt.ShouldBe(Now.AddMinutes(60));
        code.LastSentAt.ShouldBe(Now);
        code.WrongTries.ShouldBe(0);
        code.IsUsable(Now.AddMinutes(59)).ShouldBeTrue();
    }

    [Fact]
    public void IsUsable_AtTheExactExpiry_IsFalse()
    {
        var code = SignInCode.Issue(Phone, "seed-1", Now);

        code.IsUsable(Now.AddMinutes(60)).ShouldBeFalse();
    }

    [Fact]
    public void SendAgainAt_AfterASend_IsTwoMinutesLater()
    {
        var code = SignInCode.Issue(Phone, "seed-1", Now);

        code.SendAgainAt.ShouldBe(Now.AddMinutes(2));
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(30, 30)]
    [InlineData(59.5, 1)]
    public void MinutesLeft_PartWayThrough_RoundsUp(double minutesGone, int minutesLeft)
    {
        var code = SignInCode.Issue(Phone, "seed-1", Now);

        code.MinutesLeft(Now.AddMinutes(minutesGone)).ShouldBe(minutesLeft);
    }

    // ---- Reissue ----

    [Fact]
    public void Reissue_WhileTheCodeStillWorks_IsRefused()
    {
        var code = SignInCode.Issue(Phone, "seed-1", Now);

        var result = code.Reissue("seed-2", Now.AddMinutes(10));

        result.Error.ShouldBe(SignInCodeErrors.StillValid);
        code.CodeSeed.ShouldBe("seed-1");
    }

    [Fact]
    public void Reissue_AfterExpiry_StartsANewCodeWithFreshTries()
    {
        var code = SignInCode.Issue(Phone, "seed-1", Now);
        code.Verify("00000", Code, Now.AddMinutes(1));
        var later = Now.AddMinutes(61);

        var result = code.Reissue("seed-2", later);

        result.IsSuccess.ShouldBeTrue();
        code.CodeSeed.ShouldBe("seed-2");
        code.IssuedAt.ShouldBe(later);
        code.ExpiresAt.ShouldBe(later.AddMinutes(60));
        code.WrongTries.ShouldBe(0);
        code.IsUsable(later).ShouldBeTrue();
    }

    [Fact]
    public void Reissue_AfterTheCodeWasUsed_StartsANewCode()
    {
        var code = SignInCode.Issue(Phone, "seed-1", Now);
        code.Verify(Code, Code, Now.AddMinutes(1));

        var result = code.Reissue("seed-2", Now.AddMinutes(5));

        result.IsSuccess.ShouldBeTrue();
        code.UsedAt.ShouldBeNull();
    }

    // ---- SendAgain ----

    [Fact]
    public void SendAgain_BeforeTwoMinutes_IsRefused()
    {
        var code = SignInCode.Issue(Phone, "seed-1", Now);

        code.SendAgain(Now.AddSeconds(119)).Error.ShouldBe(SignInCodeErrors.SendAgainTooSoon);
    }

    [Fact]
    public void SendAgain_AfterTwoMinutes_KeepsTheCodeAndItsExpiry()
    {
        var code = SignInCode.Issue(Phone, "seed-1", Now);
        code.RecordProviderMessage(8792343);
        var later = Now.AddMinutes(2);

        var result = code.SendAgain(later);

        result.IsSuccess.ShouldBeTrue();
        code.CodeSeed.ShouldBe("seed-1");
        code.ExpiresAt.ShouldBe(Now.AddMinutes(60));
        code.LastSentAt.ShouldBe(later);
        code.LastProviderMessageId.ShouldBeNull();
        code.SendAgainAt.ShouldBe(later.AddMinutes(2));
    }

    [Fact]
    public void SendAgain_AfterExpiry_IsRefused()
    {
        var code = SignInCode.Issue(Phone, "seed-1", Now);

        code.SendAgain(Now.AddMinutes(60)).Error.ShouldBe(SignInCodeErrors.Expired);
    }

    // ---- Verify ----

    [Fact]
    public void Verify_RightCode_SucceedsAndUsesItUp()
    {
        var code = SignInCode.Issue(Phone, "seed-1", Now);
        var later = Now.AddMinutes(50);

        var result = code.Verify(Code, Code, later);

        result.IsSuccess.ShouldBeTrue();
        code.UsedAt.ShouldBe(later);
        code.IsUsable(later).ShouldBeFalse();
    }

    [Fact]
    public void Verify_RightCodeTwice_TheSecondIsRefused()
    {
        var code = SignInCode.Issue(Phone, "seed-1", Now);
        code.Verify(Code, Code, Now.AddMinutes(1));

        code.Verify(Code, Code, Now.AddMinutes(2)).Error.ShouldBe(SignInCodeErrors.Expired);
    }

    [Fact]
    public void Verify_WrongCode_CountsATry()
    {
        var code = SignInCode.Issue(Phone, "seed-1", Now);

        var result = code.Verify("04218", Code, Now.AddMinutes(1));

        result.Error.ShouldBe(SignInCodeErrors.Wrong);
        code.WrongTries.ShouldBe(1);
        code.IsUsable(Now.AddMinutes(1)).ShouldBeTrue();
    }

    [Fact]
    public void Verify_FifthWrongCode_EndsTheCode()
    {
        var code = SignInCode.Issue(Phone, "seed-1", Now);
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            code.Verify("11111", Code, Now.AddMinutes(1)).Error.ShouldBe(SignInCodeErrors.Wrong);
        }

        var fifth = code.Verify("11111", Code, Now.AddMinutes(1));

        fifth.Error.ShouldBe(SignInCodeErrors.TooManyWrongTries);
        code.WrongTries.ShouldBe(SignInCode.MaxWrongTries);
        code.Verify(Code, Code, Now.AddMinutes(2)).Error.ShouldBe(SignInCodeErrors.Expired);
    }

    [Fact]
    public void Verify_AfterExpiry_IsRefusedWithoutCountingATry()
    {
        var code = SignInCode.Issue(Phone, "seed-1", Now);

        code.Verify(Code, Code, Now.AddMinutes(60)).Error.ShouldBe(SignInCodeErrors.Expired);
        code.WrongTries.ShouldBe(0);
    }

    [Fact]
    public void Verify_CodeOfAnotherLength_IsWrong()
    {
        var code = SignInCode.Issue(Phone, "seed-1", Now);

        code.Verify("4217", Code, Now.AddMinutes(1)).Error.ShouldBe(SignInCodeErrors.Wrong);
    }

    // ---- Limits ----

    [Fact]
    public void CheckSendLimits_UnderBoth_Passes()
    {
        SignInCode.CheckSendLimits(phoneSendsInLastHour: 4, gymSendsToday: 299).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void CheckSendLimits_FiveSendsToThePhoneInAnHour_IsRefused()
    {
        SignInCode.CheckSendLimits(phoneSendsInLastHour: 5, gymSendsToday: 10)
            .Error.ShouldBe(SignInCodeErrors.PhoneHourlyLimitReached);
    }

    [Fact]
    public void CheckSendLimits_ThreeHundredSendsToday_IsRefused()
    {
        SignInCode.CheckSendLimits(phoneSendsInLastHour: 0, gymSendsToday: 300)
            .Error.ShouldBe(SignInCodeErrors.GymDailyLimitReached);
    }
}
