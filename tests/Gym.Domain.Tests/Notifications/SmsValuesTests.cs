using Gym.Domain.Notifications;
using Gym.Domain.Payables;

namespace Gym.Domain.Tests.Notifications;

/// <summary>The values in each template's blanks, written the way the app shows them (docs/sms-templates.md).</summary>
public sealed class SmsValuesTests
{
    private const string LongName = "سید محمد حسین علی رضا موسوی تبریزی نژاد";
    private const string LongPayee = "شرکت بازرگانی تجهیزات ورزشی و بدنسازی پارس پویا نوین گستر آریا";

    [Fact]
    public void Date_GregorianDay_IsTheJalaliDateInPersianDigits()
    {
        SmsValues.Date(new DateOnly(2026, 10, 12)).ShouldBe("۱۴۰۵/۰۷/۲۰");
    }

    [Fact]
    public void Number_Digits_ArePersian()
    {
        SmsValues.Number(12).ShouldBe("۱۲");
    }

    [Theory]
    [InlineData("12500000", "۱۲٬۵۰۰٬۰۰۰")]
    [InlineData("12500000.00", "۱۲٬۵۰۰٬۰۰۰")]
    [InlineData("1500000.50", "۱٬۵۰۰٬۰۰۰٫۵")]
    [InlineData("500", "۵۰۰")]
    [InlineData("0", "۰")]
    public void Amount_Toman_IsGroupedInPersian(string amount, string expected)
    {
        SmsValues.Amount(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)).ShouldBe(expected);
    }

    [Fact]
    public void Amount_Negative_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => SmsValues.Amount(-1m));
    }

    [Fact]
    public void ForRunningOut_NameAndEndDate_FillToken10AndToken()
    {
        var tokens = SmsValues.ForRunningOut("سارا محمدی", new DateOnly(2026, 10, 12));

        tokens.ShouldBe(new SmsTokens("۱۴۰۵/۰۷/۲۰", Token10: "سارا محمدی"));
    }

    [Fact]
    public void ForRunningOut_LongName_IsCutToSixWords()
    {
        var tokens = SmsValues.ForRunningOut(LongName, new DateOnly(2026, 10, 12));

        tokens.Token10.ShouldBe("سید محمد حسین علی رضا موسوی");
    }

    [Fact]
    public void ForFewSessionsLeft_SessionsLeft_FillToken()
    {
        SmsValues.ForFewSessionsLeft("سارا محمدی", 2).ShouldBe(new SmsTokens("۲", Token10: "سارا محمدی"));
    }

    [Fact]
    public void ForBirthday_Birthday_FillToken()
    {
        SmsValues.ForBirthday("سارا محمدی", new DateOnly(2026, 10, 12)).ShouldBe(new SmsTokens("۱۴۰۵/۰۷/۲۰", Token10: "سارا محمدی"));
    }

    [Fact]
    public void ForPayableDue_Cheque_FillsEveryBlank()
    {
        var tokens = SmsValues.ForPayableDue(PayableKind.Cheque, 12_500_000m, new DateOnly(2026, 10, 12), "فروشگاه تجهیزات ورزشی");

        tokens.ShouldBe(new SmsTokens("چک", "۱۲٬۵۰۰٬۰۰۰", "۱۴۰۵/۰۷/۲۰", Token20: "فروشگاه تجهیزات ورزشی"));
    }

    [Fact]
    public void ForPayableDue_Installment_SaysInstalment()
    {
        SmsValues.ForPayableDue(PayableKind.Installment, 1m, new DateOnly(2026, 10, 12), "بانک").Token.ShouldBe("قسط");
    }

    [Fact]
    public void ForPayableDue_LongPayee_IsCutToNineWords()
    {
        var tokens = SmsValues.ForPayableDue(PayableKind.Cheque, 1m, new DateOnly(2026, 10, 12), LongPayee);

        tokens.Token20.ShouldBe("شرکت بازرگانی تجهیزات ورزشی و بدنسازی پارس پویا نوین");
    }
}
