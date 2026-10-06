using Gym.Domain.Notifications;

namespace Gym.Domain.Tests.Notifications;

/// <summary>Kavenegar's limits on a template's blanks (BUSINESS_RULES.md §10 <i>Sending</i>).</summary>
public sealed class SmsTokensTests
{
    [Fact]
    public void Create_ValuesWithinTheLimits_KeepsThem()
    {
        var tokens = new SmsTokens(
            "۱۴۰۵/۰۷/۲۰", "۲", "قسط", Token10: "علی رضا محمدی", Token20: "بانک ملت شعبه مرکزی تهران");

        tokens.Token.ShouldBe("۱۴۰۵/۰۷/۲۰");
        tokens.Token2.ShouldBe("۲");
        tokens.Token3.ShouldBe("قسط");
        tokens.Token10.ShouldBe("علی رضا محمدی");
        tokens.Token20.ShouldBe("بانک ملت شعبه مرکزی تهران");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_NoToken_Throws(string token)
    {
        // Kavenegar refuses a template message without %token.
        Should.Throw<ArgumentException>(() => new SmsTokens(token));
    }

    [Fact]
    public void Create_BlankOptionalValue_CountsAsNone()
    {
        new SmsTokens("۲", Token10: " ").Token10.ShouldBeNull();
    }

    [Fact]
    public void Create_SpaceInAShortToken_Throws()
    {
        Should.Throw<ArgumentException>(() => new SmsTokens("دو کلمه"));
        Should.Throw<ArgumentException>(() => new SmsTokens("۲", Token2: "دو کلمه"));
        Should.Throw<ArgumentException>(() => new SmsTokens("۲", Token3: "دو کلمه"));
    }

    [Fact]
    public void Create_Token10WithFiveSpaces_IsAllowedAndSixAreNot()
    {
        new SmsTokens("۲", Token10: "a b c d e f").Token10.ShouldBe("a b c d e f");

        Should.Throw<ArgumentException>(() => new SmsTokens("۲", Token10: "a b c d e f g"));
    }

    [Fact]
    public void Create_Token20WithEightSpaces_IsAllowedAndNineAreNot()
    {
        new SmsTokens("۲", Token20: "a b c d e f g h i").Token20.ShouldBe("a b c d e f g h i");

        Should.Throw<ArgumentException>(() => new SmsTokens("۲", Token20: "a b c d e f g h i j"));
    }

    [Fact]
    public void Create_ValueOverTheLength_Throws()
    {
        new SmsTokens(new string('x', SmsTokens.MaxLength)).Token.Length.ShouldBe(SmsTokens.MaxLength);

        Should.Throw<ArgumentException>(() => new SmsTokens(new string('x', SmsTokens.MaxLength + 1)));
    }

    [Fact]
    public void Fit_ManyWords_KeepsTheFirstOnesTheBlankAllows()
    {
        SmsTokens.Fit("بانک ملت شعبه مرکزی تهران خیابان فردوسی کوچه دوم پلاک دوازده", SmsTokens.MaxSpacesInToken20)
            .ShouldBe("بانک ملت شعبه مرکزی تهران خیابان فردوسی کوچه دوم");
    }

    [Fact]
    public void Fit_RunsOfWhiteSpace_BecomeOneSpace()
    {
        SmsTokens.Fit("  سارا \t  محمدی\n", SmsTokens.MaxSpacesInToken10).ShouldBe("سارا محمدی");
    }

    [Fact]
    public void Fit_NoSpacesAllowed_KeepsTheFirstWord()
    {
        SmsTokens.Fit("فروشگاه تجهیزات", SmsTokens.MaxSpacesInShortToken).ShouldBe("فروشگاه");
    }

    [Fact]
    public void Fit_TooLong_IsCutAtTheLimit()
    {
        var fitted = SmsTokens.Fit(new string('x', 150), SmsTokens.MaxSpacesInToken20);

        fitted.Length.ShouldBe(SmsTokens.MaxLength);
    }

    [Fact]
    public void Fit_CutEndingOnASpace_DropsIt()
    {
        var value = new string('x', SmsTokens.MaxLength - 1) + " yy";

        SmsTokens.Fit(value, SmsTokens.MaxSpacesInToken20).ShouldBe(new string('x', SmsTokens.MaxLength - 1));
    }

    [Fact]
    public void Fit_Result_IsAlwaysAcceptedByTheBlank()
    {
        var longPayee = string.Join(' ', Enumerable.Repeat("فروشگاه", 40));

        var tokens = new SmsTokens("چک", Token20: SmsTokens.Fit(longPayee, SmsTokens.MaxSpacesInToken20));

        tokens.Token20.ShouldNotBeNull();
    }
}
