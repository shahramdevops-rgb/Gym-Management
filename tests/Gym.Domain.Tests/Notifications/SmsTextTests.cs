using Gym.Domain.Notifications;
using Gym.Domain.Payables;

namespace Gym.Domain.Tests.Notifications;

/// <summary>Each SMS's whole text, with its values written the way the app shows them (docs/sms-texts.md).</summary>
public sealed class SmsTextTests
{
    private const string LongName = "سید محمد حسین علی رضا موسوی تبریزی نژاد";
    private const string LongPayee = "شرکت بازرگانی تجهیزات ورزشی و بدنسازی پارس پویا نوین گستر آریا";

    /// <summary>Kavenegar's opt-out line, on its own line at the end of every text.</summary>
    private const string OptOutLine = "\nلغو11";

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
            .ShouldBe("سارا محمدی عزیز، اشتراک شما در باشگاه پاسارگاد ۱۴۰۵/۰۷/۲۰ به پایان می‌رسد." + OptOutLine);
    }

    [Fact]
    public void ForFewSessionsLeft_SessionsLeft_IsTheAgreedText()
    {
        SmsText.ForFewSessionsLeft("سارا محمدی", 2)
            .ShouldBe("سارا محمدی عزیز، فقط ۲ جلسه از اشتراک شما در باشگاه پاسارگاد مانده است." + OptOutLine);
    }

    [Fact]
    public void ForBirthday_OnTheDayItself_SaysHappyBirthday()
    {
        SmsText.ForBirthday("سارا محمدی", Day, today: Day)
            .ShouldBe("سارا محمدی عزیز، امروز ۱۴۰۵/۰۷/۲۰ روز تولد شماست. تولدتان مبارک! باشگاه پاسارگاد" + OptOutLine);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void ForBirthday_DaysAhead_CongratulatesInAdvance(int daysAhead)
    {
        SmsText.ForBirthday("سارا محمدی", Day, today: Day.AddDays(-daysAhead))
            .ShouldBe("سارا محمدی عزیز، تولدتان در ۱۴۰۵/۰۷/۲۰ را پیشاپیش تبریک می‌گوییم. باشگاه پاسارگاد" + OptOutLine);
    }

    [Fact]
    public void ForPayableDue_Cheque_GivesKindAmountDateAndPayee()
    {
        SmsText.ForPayableDue(PayableKind.Cheque, 12_500_000m, Day, "فروشگاه تجهیزات ورزشی")
            .ShouldBe("یادآوری چک: ۱۲٬۵۰۰٬۰۰۰ تومان، سررسید ۱۴۰۵/۰۷/۲۰، به فروشگاه تجهیزات ورزشی" + OptOutLine);
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
            .ShouldEndWith("، به شرکت بازرگانی تجهیزات ورزشی و بدنسازی پارس پویا نوین" + OptOutLine);
    }

    [Fact]
    public void ForEveryKind_Text_EndsWithKavenegarsOptOutLine()
    {
        string[] texts =
        [
            SmsText.ForRunningOut("سارا محمدی", Day),
            SmsText.ForFewSessionsLeft("سارا محمدی", 2),
            SmsText.ForBirthday("سارا محمدی", Day, today: Day),
            SmsText.ForBirthday("سارا محمدی", Day, today: Day.AddDays(-3)),
            SmsText.ForPayableDue(PayableKind.Installment, 1m, Day, "بانک"),
        ];

        // Exactly as it reached the phone on 1405/07/26: its own line, English digits, no space.
        texts.ShouldAllBe(text => text.EndsWith("\nلغو11", StringComparison.Ordinal));
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

    // ---- The daily report (task 10.7) ----

    [Fact]
    public void MonthAndDay_GregorianDay_LeavesTheYearOut()
    {
        SmsText.MonthAndDay(Day).ShouldBe("۰۷/۲۰");
    }

    [Theory]
    [InlineData("12345600", "۱۲٬۳۴۶")]
    [InlineData("12345499", "۱۲٬۳۴۵")]
    [InlineData("12000000", "۱۲٬۰۰۰")]
    [InlineData("1500", "۲")]
    [InlineData("499", "۰")]
    [InlineData("0", "۰")]
    public void Thousands_Toman_IsRoundedToTheNearestThousand(string amount, string expected)
    {
        SmsText.Thousands(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)).ShouldBe(expected);
    }

    [Fact]
    public void Thousands_BelowZero_CarriesTheAppsMinusSign()
    {
        SmsText.Thousands(-1_500_000m).ShouldBe(SmsText.MinusSign + "۱٬۵۰۰");
    }

    [Fact]
    public void Thousands_SmallRefundRoundingToZero_HasNoMinusSign()
    {
        SmsText.Thousands(-400m).ShouldBe("۰");
    }

    [Fact]
    public void ForDailyReport_DayWithCash_IsTheAgreedText()
    {
        var figures = new DailyReportFigures(
            new DateOnly(2026, 10, 19), CheckIns: 85, PlansSold: 3, SingleVisitsSold: 5,
            Received: 12_345_600m, Cash: 2_000_000m, Card: 8_000_000m, BankTransfer: 2_345_600m);

        SmsText.ForDailyReport(figures).ShouldBe(
            "گزارش باشگاه پاسارگاد ۰۷/۲۷\n" +
            "ورود: ۸۵\n" +
            "پلن: ۳، تک‌جلسه: ۵\n" +
            "دریافتی: ۱۲٬۳۴۶ هزار تومان\n" +
            "نقد: ۲٬۰۰۰\n" +
            "کارت: ۸٬۰۰۰\n" +
            "انتقال: ۲٬۳۴۶" + OptOutLine);
    }

    [Fact]
    public void ForDailyReport_DayWithoutCash_LeavesTheCashLineOut()
    {
        var figures = new DailyReportFigures(
            new DateOnly(2026, 10, 19), CheckIns: 4, PlansSold: 0, SingleVisitsSold: 0,
            Received: 900_000m, Cash: 0m, Card: 900_000m, BankTransfer: 0m);

        var text = SmsText.ForDailyReport(figures);

        text.ShouldNotContain("نقد");
        text.ShouldContain("کارت: ۹۰۰\nانتقال: ۰" + OptOutLine);
    }

    [Fact]
    public void ForDailyReport_BusyDay_FitsInTwoParts()
    {
        // Two parts hold 2 × 67 characters (§10 Sending): the reason the amounts are in thousands.
        var figures = new DailyReportFigures(
            new DateOnly(2026, 10, 19), CheckIns: 250, PlansSold: 20, SingleVisitsSold: 30,
            Received: 150_000_000m, Cash: 20_000_000m, Card: 100_000_000m, BankTransfer: 30_000_000m);

        SmsText.ForDailyReport(figures).Length.ShouldBeLessThanOrEqualTo(2 * 67);
    }

    [Fact]
    public void IsEmpty_NothingHappened_IsTrue()
    {
        new DailyReportFigures(Day, 0, 0, 0, 0m, 0m, 0m, 0m).IsEmpty().ShouldBeTrue();
    }

    [Theory]
    [InlineData(1, 0, 0, "0")]
    [InlineData(0, 1, 0, "0")]
    [InlineData(0, 0, 1, "0")]
    [InlineData(0, 0, 0, "-1000")]
    public void IsEmpty_AnyFigure_IsFalse(int checkIns, int plans, int singleVisits, string received)
    {
        var amount = decimal.Parse(received, System.Globalization.CultureInfo.InvariantCulture);

        new DailyReportFigures(Day, checkIns, plans, singleVisits, amount, amount, 0m, 0m).IsEmpty().ShouldBeFalse();
    }
}
