using Gym.Domain.Notifications;
using Gym.Domain.Payables;
using Gym.Domain.Subscriptions;

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
    public void ForRunningOut_NameEndDateAndSessionsLeft_IsTheAgreedText()
    {
        // The sessions left were added by the developer on 1405/08/01 (task 10.17).
        SmsText.ForRunningOut("سارا محمدی", Day, 12)
            .ShouldBe("سارا عزیز، اشتراک شما در باشگاه پاسارگاد ۱۴۰۵/۰۷/۲۰ به پایان می‌رسد و فقط ۱۲ جلسه از آن مانده است." + OptOutLine);
    }

    [Fact]
    public void ForRunningOut_ShortName_IsStillTwoParts()
    {
        // Two parts before the sessions were added too: the longer text costs nothing more.
        SmsParts.Count(SmsText.ForRunningOut("علی", Day, 12)).ShouldBe(2);
    }

    [Fact]
    public void ForFewSessionsLeft_SessionsLeft_IsTheAgreedText()
    {
        SmsText.ForFewSessionsLeft("سارا محمدی", 2)
            .ShouldBe("سارا عزیز، فقط ۲ جلسه از اشتراک شما در باشگاه پاسارگاد مانده است." + OptOutLine);
    }

    [Fact]
    public void ForBirthday_Birthday_IsTheAgreedTextWithTheFreeVisit()
    {
        SmsText.ForBirthday("سارا محمدی", Day)
            .ShouldBe("سارا عزیز سلام، ۰۷/۲۰ روز تولدته؛ اون روز مهمون باشگاه پاسارگاد هستی و ورودت رایگانه. پیشاپیش تولدت مبارک رفیق!!" + OptOutLine);
    }

    [Fact]
    public void ForBirthday_TenCharacterName_FitsInTwoParts()
    {
        // docs/sms-texts.md: about 118 characters; two parts hold 134.
        SmsText.ForBirthday("سارا محمدی", Day).Length.ShouldBeLessThanOrEqualTo(2 * 67);
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
    public void ForRunningOut_NameWithSayyed_IsTheWordAfterIt()
    {
        SmsText.ForRunningOut(LongName, Day, 3).ShouldStartWith("محمد عزیز،");
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
            SmsText.ForRunningOut("سارا محمدی", Day, 3),
            SmsText.ForFewSessionsLeft("سارا محمدی", 2),
            SmsText.ForBirthday("سارا محمدی", Day),
            SmsText.ForPayableDue(PayableKind.Installment, 1m, Day, "بانک"),
        ];

        // Exactly as it reached the phone on 1405/07/26: its own line, English digits, no space.
        texts.ShouldAllBe(text => text.EndsWith("\nلغو11", StringComparison.Ordinal));
    }

    [Fact]
    public void ForEveryKind_TheLongestValues_FitInAMessage()
    {
        var longest = string.Join(' ', Enumerable.Repeat(new string('x', 40), 20));

        SmsText.ForRunningOut(longest, Day, Subscription.MaxSessionCount).Length.ShouldBeLessThanOrEqualTo(SmsText.MaxLength);
        SmsText.ForBirthday(longest, Day).Length.ShouldBeLessThanOrEqualTo(SmsText.MaxLength);
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
        SmsText.Fit("  بانک \t  ملت\n", SmsText.MaxPayeeWords).ShouldBe("بانک ملت");
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

    // ---- The subscription receipt (task 10.9) ----

    [Fact]
    public void ForSubscriptionReceipt_Plan_GivesSessionsDatesWithoutTheYearAndPrice()
    {
        SmsText.ForSubscriptionReceipt("سارا محمدی", 12, Day, Day.AddDays(29), 1_200_000m)
            .ShouldBe("سارا عزیز، اشتراک ۱۲ جلسه‌ای شما در باشگاه پاسارگاد از ۰۷/۲۰ تا ۰۸/۱۹ ثبت و ۱٬۲۰۰٬۰۰۰ تومان کامل پرداخت شد. سپاس!" + OptOutLine);
    }

    [Fact]
    public void ForQueuedSubscriptionReceipt_Plan_GivesItsDaysInsteadOfDates()
    {
        SmsText.ForQueuedSubscriptionReceipt("سارا محمدی", 12, 30, 1_200_000m)
            .ShouldBe("سارا عزیز، اشتراک ۱۲ جلسه‌ای شما در باشگاه پاسارگاد (۳۰ روز، پس از اشتراک فعلی) ثبت و ۱٬۲۰۰٬۰۰۰ تومان کامل پرداخت شد. سپاس!" + OptOutLine);
    }

    [Fact]
    public void ForSubscriptionReceipt_NameWithSayyed_IsTheFirstNameOnly()
    {
        var text = SmsText.ForSubscriptionReceipt(LongName, 12, Day, Day.AddDays(29), 1_200_000m);

        text.ShouldStartWith("محمد عزیز،");
    }

    [Fact]
    public void ForSubscriptionReceipt_TenCharacterName_FitsInTwoParts()
    {
        // docs/sms-texts.md: about 125 characters, two parts of 67 (why «مبلغ» was taken out).
        var text = SmsText.ForSubscriptionReceipt("سارا محمدی", 12, Day, Day.AddDays(29), 1_200_000m);

        text.Length.ShouldBeLessThanOrEqualTo(2 * 67);
    }

    // ---- The welcome (task 10.10) ----

    [Fact]
    public void ForWelcome_Member_GreetsThemByName()
    {
        SmsText.ForWelcome("سارا محمدی")
            .ShouldBe("درود بر سارا عزیز، به باشگاه پاسارگاد خوش اومدی، امیدوارم ساعات خوشی رو توی باشگاه داشته باشی" + OptOutLine);
    }

    [Fact]
    public void ForWelcome_NameWithSayyed_IsTheFirstNameOnly()
    {
        SmsText.ForWelcome(LongName).ShouldStartWith("درود بر محمد عزیز،");
    }

    [Fact]
    public void ForWelcome_TenCharacterName_IsTwoParts()
    {
        // docs/sms-texts.md: about 99 characters, two parts of 67 (the developer accepted two, 1405/08/01).
        SmsParts.Count(SmsText.ForWelcome("سارا محمدی")).ShouldBe(2);
    }

    // ---- The welcome back (task 10.20) ----

    [Fact]
    public void ForWelcomeBack_FirstTurn_IsTheFirstTextWithTheName()
    {
        SmsText.ForWelcomeBack("سارا محمدی", turn: 0).ShouldBe(
            "سارا عزیز، از اینکه به باشگاه پاسارگاد برگشتی خیلی خوشحالیم! هیچی مثل ورزش حال آدم رو خوب نمی‌کنه."
            + OptOutLine);
    }

    [Fact]
    public void ForWelcomeBack_EachTurn_IsTheNextText()
    {
        var texts = Enumerable.Range(0, SmsText.WelcomeBackTexts.Count)
            .Select(turn => SmsText.ForWelcomeBack("سارا محمدی", turn))
            .ToList();

        texts.Distinct().Count().ShouldBe(6);
        texts[5].ShouldBe("سلام سارا عزیز، چه خوب که دوباره اومدی! جات اینجا همیشه محفوظه، با قدرت ادامه بده." + OptOutLine);
    }

    [Fact]
    public void ForWelcomeBack_AfterTheSixth_StartsAgain()
    {
        SmsText.ForWelcomeBack("سارا محمدی", turn: 6).ShouldBe(SmsText.ForWelcomeBack("سارا محمدی", turn: 0));
        SmsText.ForWelcomeBack("سارا محمدی", turn: 13).ShouldBe(SmsText.ForWelcomeBack("سارا محمدی", turn: 1));
    }

    [Fact]
    public void ForWelcomeBack_NameWithSayyed_IsTheFirstNameOnly()
    {
        SmsText.ForWelcomeBack(LongName, turn: 4).ShouldStartWith("سلام محمد عزیز،");
    }

    [Fact]
    public void ForWelcomeBack_EveryTextWithATenCharacterName_IsTwoParts()
    {
        // docs/sms-texts.md: every one of the six takes two parts.
        for (var turn = 0; turn < SmsText.WelcomeBackTexts.Count; turn++)
        {
            SmsParts.Count(SmsText.ForWelcomeBack("سارا محمدی", turn)).ShouldBe(2);
        }
    }

    [Fact]
    public void ForWelcomeBack_NegativeTurn_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => SmsText.ForWelcomeBack("سارا محمدی", turn: -1));
    }

    // ---- The debt reminder (task 10.11) ----

    [Fact]
    public void ForDebtReminder_Member_GivesTheWholeAmountPolitely()
    {
        SmsText.ForDebtReminder("سارا محمدی", 1_250_000m).ShouldBe(
            "سارا عزیز، مانده حساب شما در باشگاه پاسارگاد ۱٬۲۵۰٬۰۰۰ تومان است. لطفاً در مراجعه بعدی تسویه بفرمایید. سپاس!"
            + OptOutLine);
    }

    [Fact]
    public void ForDebtReminder_NameWithSayyed_IsTheFirstNameOnly()
    {
        SmsText.ForDebtReminder(LongName, 50_000m).ShouldStartWith("محمد عزیز،");
    }

    [Fact]
    public void ForDebtReminder_TenCharacterName_FitsInTwoParts()
    {
        // docs/sms-texts.md: about 120 characters, two parts of 67.
        SmsText.ForDebtReminder("سارا محمدی", 1_200_000m).Length.ShouldBeLessThanOrEqualTo(2 * 67);
    }

    // ---- The absence SMS (task 10.12) ----

    [Fact]
    public void ForAbsence_Member_TellsThemTheirPlaceIsEmptyAndTheDaysAway()
    {
        SmsText.ForAbsence("سارا محمدی", 12)
            .ShouldBe("سارا عزیز، جات توی باشگاه پاسارگاد خالیه! ۱۲ روزه که باشگاه نیومدی و منتظر حضور گرمت هستیم!" + OptOutLine);
    }

    [Fact]
    public void ForAbsence_NameWithSayyed_IsTheFirstNameOnly()
    {
        SmsText.ForAbsence(LongName, 12).ShouldStartWith("محمد عزیز،");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ForAbsence_NoDaysAway_Throws(int daysAway)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => SmsText.ForAbsence("سارا محمدی", daysAway));
    }

    [Fact]
    public void ForAbsence_TenCharacterName_IsTwoParts()
    {
        // docs/sms-texts.md: about 106 characters, two parts of 67 (the developer accepted two, 1405/08/01).
        SmsParts.Count(SmsText.ForAbsence("سارا محمدی", 60)).ShouldBe(2);
    }

    // ---- The first name (task 10.13) ----

    [Theory]
    [InlineData("علی رضایی", "علی")]
    [InlineData("علی", "علی")]
    [InlineData("  علی   رضایی  ", "علی")]
    [InlineData("سید محمد احمدی", "محمد")]
    [InlineData("سيد محمد احمدی", "محمد")]
    [InlineData("سید", "سید")]
    [InlineData("محمد علی کریمی", "محمد")]
    [InlineData("محمدعلی کریمی", "محمدعلی")]
    public void FirstName_FullName_IsTheFirstWordOrTheOneAfterSayyed(string fullName, string expected)
    {
        SmsText.FirstName(fullName).ShouldBe(expected);
    }

    [Fact]
    public void FirstName_VeryLongWord_IsCutAtTheLimit()
    {
        SmsText.FirstName(new string('x', 150) + " رضایی").Length.ShouldBe(SmsText.MaxValueLength);
    }

    [Fact]
    public void FirstName_Blank_IsEmpty()
    {
        SmsText.FirstName("   ").ShouldBe(string.Empty);
    }

    // ---- The broadcast (task 10.13) ----

    [Fact]
    public void ForBroadcast_OwnersText_IsBetweenTheFirstNameAndTheOptOutLine()
    {
        SmsText.ForBroadcast("سید محمد احمدی", "باشگاه فردا تا ساعت ۱۶ تعطیل است.")
            .ShouldBe("محمد عزیز، باشگاه فردا تا ساعت ۱۶ تعطیل است." + OptOutLine);
    }

    [Fact]
    public void ForBroadcast_LineBreaks_AreKept()
    {
        SmsText.ForBroadcast("علی رضایی", "خط اول\nخط دوم").ShouldBe("علی عزیز، خط اول\nخط دوم" + OptOutLine);
    }

    [Fact]
    public void ForBroadcast_BlankText_Throws()
    {
        Should.Throw<ArgumentException>(() => SmsText.ForBroadcast("علی", "  "));
    }
}
