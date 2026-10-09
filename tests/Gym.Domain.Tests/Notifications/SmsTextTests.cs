using Gym.Domain.Notifications;
using Gym.Domain.Payables;

namespace Gym.Domain.Tests.Notifications;

/// <summary>Each SMS's whole text, with its values written the way the app shows them (docs/sms-texts.md).</summary>
public sealed class SmsTextTests
{
    private const string LongName = "سید محمد حسین علی رضا موسوی تبریزی نژاد";
    private const string LongPayee = "شرکت بازرگانی تجهیزات ورزشی و بدنسازی پارس پویا نوین گستر آریا";

    private static readonly DateOnly Day = new(2026, 10, 12);

    // ---- The values ----

    [Fact]
    public void Date_GregorianDay_IsTheJalaliDateInPersianDigits()
    {
        SmsText.Date(Day).ShouldBe("۱۴۰۵/۰۷/۲۰");
    }

    [Fact]
    public void Number_Digits_ArePersian()
    {
        SmsText.Number(12).ShouldBe("۱۲");
    }

    [Theory]
    [InlineData("12500000", "۱۲٬۵۰۰٬۰۰۰")]
    [InlineData("12500000.00", "۱۲٬۵۰۰٬۰۰۰")]
    [InlineData("1500000.50", "۱٬۵۰۰٬۰۰۰٫۵")]
    [InlineData("500", "۵۰۰")]
    [InlineData("0", "۰")]
    public void Amount_Toman_IsGroupedInPersian(string amount, string expected)
    {
        SmsText.Amount(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)).ShouldBe(expected);
    }

    [Fact]
    public void Amount_Negative_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => SmsText.Amount(-1m));
    }

    // ---- The four texts ----

    [Fact]
    public void ForRunningOut_NameAndEndDate_IsTheAgreedText()
    {
        SmsText.ForRunningOut("سارا محمدی", Day)
            .ShouldBe("سارا محمدی عزیز، اشتراک شما در باشگاه پاسارگاد ۱۴۰۵/۰۷/۲۰ به پایان می‌رسد.");
    }

    [Fact]
    public void ForFewSessionsLeft_SessionsLeft_IsTheAgreedText()
    {
        SmsText.ForFewSessionsLeft("سارا محمدی", 2)
            .ShouldBe("سارا محمدی عزیز، فقط ۲ جلسه از اشتراک شما در باشگاه پاسارگاد مانده است.");
    }

    [Fact]
    public void ForBirthday_OnTheDayItself_SaysHappyBirthday()
    {
        SmsText.ForBirthday("سارا محمدی", Day, today: Day)
            .ShouldBe("سارا محمدی عزیز، امروز ۱۴۰۵/۰۷/۲۰ روز تولد شماست. تولدتان مبارک! باشگاه پاسارگاد");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void ForBirthday_DaysAhead_CongratulatesInAdvance(int daysAhead)
    {
        SmsText.ForBirthday("سارا محمدی", Day, today: Day.AddDays(-daysAhead))
            .ShouldBe("سارا محمدی عزیز، تولدتان در ۱۴۰۵/۰۷/۲۰ را پیشاپیش تبریک می‌گوییم. باشگاه پاسارگاد");
    }

    [Fact]
    public void ForPayableDue_Cheque_GivesKindAmountDateAndPayee()
    {
        SmsText.ForPayableDue(PayableKind.Cheque, 12_500_000m, Day, "فروشگاه تجهیزات ورزشی")
            .ShouldBe("یادآوری چک: ۱۲٬۵۰۰٬۰۰۰ تومان، سررسید ۱۴۰۵/۰۷/۲۰، به فروشگاه تجهیزات ورزشی");
    }

    [Fact]
    public void ForPayableDue_Installment_SaysInstalment()
    {
        SmsText.ForPayableDue(PayableKind.Installment, 1m, Day, "بانک").ShouldStartWith("یادآوری قسط: ");
    }

    [Fact]
    public void ForRunningOut_LongName_IsCutToSixWords()
    {
        SmsText.ForRunningOut(LongName, Day).ShouldStartWith("سید محمد حسین علی رضا موسوی عزیز،");
    }

    [Fact]
    public void ForPayableDue_LongPayee_IsCutToNineWords()
    {
        SmsText.ForPayableDue(PayableKind.Cheque, 1m, Day, LongPayee)
            .ShouldEndWith("، به شرکت بازرگانی تجهیزات ورزشی و بدنسازی پارس پویا نوین");
    }

    [Fact]
    public void ForEveryKind_TheLongestValues_FitInAMessage()
    {
        var longest = string.Join(' ', Enumerable.Repeat(new string('x', 40), 20));

        SmsText.ForRunningOut(longest, Day).Length.ShouldBeLessThanOrEqualTo(SmsText.MaxLength);
        SmsText.ForBirthday(longest, Day, Day.AddDays(-1)).Length.ShouldBeLessThanOrEqualTo(SmsText.MaxLength);
        SmsText.ForPayableDue(PayableKind.Cheque, 999_999_999_999m, Day, longest).Length
            .ShouldBeLessThanOrEqualTo(SmsText.MaxLength);
    }

    // ---- Fit ----

    [Fact]
    public void Fit_ManyWords_KeepsTheFirstOnes()
    {
        SmsText.Fit("بانک ملت شعبه مرکزی تهران خیابان فردوسی کوچه دوم پلاک دوازده", SmsText.MaxPayeeWords)
            .ShouldBe("بانک ملت شعبه مرکزی تهران خیابان فردوسی کوچه دوم");
    }

    [Fact]
    public void Fit_RunsOfWhiteSpace_BecomeOneSpace()
    {
        SmsText.Fit("  سارا \t  محمدی\n", SmsText.MaxNameWords).ShouldBe("سارا محمدی");
    }

    [Fact]
    public void Fit_TooLong_IsCutAtTheLimit()
    {
        SmsText.Fit(new string('x', 150), SmsText.MaxPayeeWords).Length.ShouldBe(SmsText.MaxValueLength);
    }

    [Fact]
    public void Fit_CutEndingOnASpace_DropsIt()
    {
        var value = new string('x', SmsText.MaxValueLength - 1) + " yy";

        SmsText.Fit(value, SmsText.MaxPayeeWords).ShouldBe(new string('x', SmsText.MaxValueLength - 1));
    }
}
