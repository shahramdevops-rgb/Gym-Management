using System.Globalization;
using System.Text;

using Gym.Domain.Common;
using Gym.Domain.Common.Text;
using Gym.Domain.Payables;

namespace Gym.Domain.Notifications;

/// <summary>
/// The whole text of each SMS (BUSINESS_RULES.md §10 <i>Sending</i>, <c>docs/sms-texts.md</c>), with
/// its values written the way the app shows them: Persian digits, a Jalali date as <c>۱۴۰۵/۰۷/۲۰</c>,
/// an amount with <c>٬</c> between thousands.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the wording is here and not in a setting.</b> Kavenegar refused the templates, so the gym
/// sends free text from its own line (1405/07/16). The developer chose to keep the agreed wording in
/// the code: it rarely changes, and a change is then a release that tests have checked, not a box
/// the Owner could leave half written.
/// </para>
/// <para>
/// <b>A member is named by the first name only</b> (<see cref="FirstName"/>, task 10.13), and a long
/// payee is cut short (<see cref="Fit"/>), as it was in the templates' blanks, so one long name
/// cannot make a message cost several parts.
/// </para>
/// <para>
/// <b>Every text ends with <see cref="OptOut"/> on its own line</b> (1405/07/26). Kavenegar adds it
/// to every message from the gym's line anyway; written here, the history keeps the text exactly as
/// it reached the phone.
/// </para>
/// </remarks>
public static class SmsText
{
    /// <summary>The longest text the kinds can make: a 100-character name or payee and the wording.</summary>
    public const int MaxLength = 500;

    /// <summary>The most characters a first name or a payee keeps.</summary>
    public const int MaxValueLength = 100;

    /// <summary>
    /// The title a first name may start with: «سید محمد احمدی» is named «محمد» (the developer,
    /// 1405/07/27). The gym is for men only, so «سیده» never comes.
    /// </summary>
    public const string SayyedTitle = "سید";

    /// <summary>A payee keeps its first 9 words, as the template's payee blank allowed.</summary>
    public const int MaxPayeeWords = 9;

    /// <summary>The Persian thousands separator (U+066C), as <c>formatMoney</c> in the frontend writes it.</summary>
    public const char ThousandsSeparator = '٬';

    /// <summary>The Persian decimal separator (U+066B).</summary>
    public const char DecimalSeparator = '٫';

    /// <summary>The word for a cheque and an instalment in the Owner's reminder.</summary>
    public const string ChequeWord = "چک";

    public const string InstallmentWord = "قسط";

    /// <summary>
    /// The opt-out line every message ends with, exactly as Kavenegar writes it: English digits, no
    /// space. Written differently, Kavenegar may not see it and add its own.
    /// </summary>
    public const string OptOut = "لغو11";

    /// <summary>The left-to-right mark (U+200E) and the minus sign (U+2212), as <c>formatMoney</c> in the frontend writes them.</summary>
    public const string MinusSign = "\u200E\u2212";

    // ---- The four kinds (docs/sms-texts.md) ----

    /// <summary>
    /// The subscription running out: the end date, not the days left, and the sessions still left on
    /// the day it is written (the developer, 1405/08/01, task 10.17). Two SMS parts, as before it.
    /// </summary>
    public static string ForRunningOut(string fullName, DateOnly endDate, int sessionsLeft) =>
        WithOptOut($"{FirstName(fullName)} عزیز، اشتراک شما در باشگاه پاسارگاد {Date(endDate)} به پایان می‌رسد و فقط {Number(sessionsLeft)} جلسه از آن مانده است.");

    public static string ForFewSessionsLeft(string fullName, int sessionsLeft) =>
        WithOptOut($"{FirstName(fullName)} عزیز، فقط {Number(sessionsLeft)} جلسه از اشتراک شما در باشگاه پاسارگاد مانده است.");

    /// <summary>
    /// Always sent 1 to 7 days early, so one text: the date without the year, the free visit on it
    /// (BUSINESS_RULES.md §7 <i>Birthday visit</i>) and «پیشاپیش تولدت مبارک» (chosen by the
    /// developer, 1405/07/27, task 10.8; the developer's spoken wording since 1405/08/01).
    /// </summary>
    /// <param name="birthday">This year's birthday, as <c>SmsAudience.BirthdayToGreet</c> found it.</param>
    public static string ForBirthday(string fullName, DateOnly birthday) =>
        WithOptOut($"{FirstName(fullName)} عزیز سلام، {MonthAndDay(birthday)} روز تولدته؛ اون روز مهمون باشگاه پاسارگاد هستی و ورودت رایگانه. پیشاپیش تولدت مبارک رفیق!!");

    /// <summary>To the Owner: the kind, the amount, the date and the payee.</summary>
    public static string ForPayableDue(PayableKind kind, decimal amount, DateOnly dueDate, string payee)
    {
        ArgumentNullException.ThrowIfNull(payee);

        var word = kind == PayableKind.Cheque ? ChequeWord : InstallmentWord;

        return WithOptOut($"یادآوری {word}: {Amount(amount)} تومان، سررسید {Date(dueDate)}، به {Fit(payee, MaxPayeeWords)}");
    }

    /// <summary>
    /// To the Owner: the day's check-ins, plans and single visits sold, and «دریافتی» with its
    /// methods, in thousands of Toman (decided with the developer, 1405/07/27, task 10.7). The cash
    /// line is left out on a day with no cash; card and transfer are always there.
    /// </summary>
    public static string ForDailyReport(DailyReportFigures figures)
    {
        ArgumentNullException.ThrowIfNull(figures);

        var lines = new List<string>
        {
            $"گزارش باشگاه پاسارگاد {MonthAndDay(figures.Day)}",
            $"ورود: {Number(figures.CheckIns)}",
            $"پلن: {Number(figures.PlansSold)}، تک‌جلسه: {Number(figures.SingleVisitsSold)}",
            $"دریافتی: {Thousands(figures.Received)} هزار تومان",
        };

        if (figures.Cash != 0m)
        {
            lines.Add($"نقد: {Thousands(figures.Cash)}");
        }

        lines.Add($"کارت: {Thousands(figures.Card)}");
        lines.Add($"انتقال: {Thousands(figures.BankTransfer)}");

        return WithOptOut(string.Join('\n', lines));
    }

    /// <summary>
    /// To the member when a plan is fully paid (task 10.9): its sessions, its dates without the year
    /// (chosen by the developer, 1405/07/27) and the price paid.
    /// </summary>
    public static string ForSubscriptionReceipt(
        string fullName, int sessions, DateOnly startDate, DateOnly endDate, decimal price) =>
        WithOptOut($"{FirstName(fullName)} عزیز، اشتراک {Number(sessions)} جلسه‌ای شما در باشگاه پاسارگاد از {MonthAndDay(startDate)} تا {MonthAndDay(endDate)} ثبت و {Amount(price)} تومان کامل پرداخت شد. سپاس!");

    /// <summary>
    /// The receipt of a plan queued behind another (BUSINESS_RULES.md §4): its dates move if the plan
    /// before it ends early or is frozen, so it gives its length instead of a promise.
    /// </summary>
    public static string ForQueuedSubscriptionReceipt(string fullName, int sessions, int days, decimal price) =>
        WithOptOut($"{FirstName(fullName)} عزیز، اشتراک {Number(sessions)} جلسه‌ای شما در باشگاه پاسارگاد ({Number(days)} روز، پس از اشتراک فعلی) ثبت و {Amount(price)} تومان کامل پرداخت شد. سپاس!");

    /// <summary>
    /// To the member when their first plan is sold (task 10.10): nothing in it that would be wrong
    /// for a member moved over from the paper notebook (chosen by the developer, 1405/07/27). The
    /// developer's warmer wording since 1405/08/01, two SMS parts.
    /// </summary>
    public static string ForWelcome(string fullName) =>
        WithOptOut($"درود بر {FirstName(fullName)} عزیز، به باشگاه پاسارگاد خوش اومدی، امیدوارم ساعات خوشی رو توی باشگاه داشته باشی");

    /// <summary>
    /// To the member whose debt has been owed for a while (task 10.11): their whole open account,
    /// asked politely (chosen by the developer, 1405/07/27).
    /// </summary>
    /// <param name="owed">The whole open account, in Toman.</param>
    public static string ForDebtReminder(string fullName, decimal owed) =>
        WithOptOut($"{FirstName(fullName)} عزیز، مانده حساب شما در باشگاه پاسارگاد {Amount(owed)} تومان است. لطفاً در مراجعه بعدی تسویه بفرمایید. سپاس!");

    /// <summary>
    /// To the member with a usable plan who has stopped coming (task 10.12): friendly, with no offer
    /// in it. Since 1405/08/01 the developer's warmer wording with the real days away, two SMS parts.
    /// </summary>
    /// <param name="daysAway">Whole days since the absence began (<c>Absence.DaysAway</c>), not the settings' number.</param>
    public static string ForAbsence(string fullName, int daysAway)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(daysAway);

        return WithOptOut($"{FirstName(fullName)} عزیز، جات توی باشگاه پاسارگاد خالیه! {Number(daysAway)} روزه که باشگاه نیومدی و منتظر حضور گرمت هستیم!");
    }

    /// <summary>
    /// The welcome back's six texts (task 10.20), each with <c>{0}</c> for the first name, chosen by
    /// the developer on 1405/08/04. A member gets them in turn (<see cref="ForWelcomeBack"/>), so the
    /// gym never sends the same automatic text twice in a row.
    /// </summary>
    public static readonly IReadOnlyList<string> WelcomeBackTexts =
    [
        "{0} عزیز، از اینکه به باشگاه پاسارگاد برگشتی خیلی خوشحالیم! هیچی مثل ورزش حال آدم رو خوب نمی‌کنه.",
        "{0} جان، خوش برگشتی! باشگاه پاسارگاد بدون تو یه چیزی کم داشت. قوی ادامه بده!",
        "{0} عزیز، دیدن دوباره‌ت توی باشگاه پاسارگاد حالمون رو خوب کرد! همین که اومدی یعنی نصف راه رو رفتی.",
        "{0} عزیز، برگشتنت رو تبریک می‌گیم! هر جلسه یه قدم به نسخهٔ بهتر خودت نزدیک‌تره.",
        "سلام {0} عزیز، چه خوب که دوباره اومدی! از امروز دوباره با هم ادامه می‌دیم، قدم به قدم.",
        "سلام {0} عزیز، چه خوب که دوباره اومدی! جات اینجا همیشه محفوظه، با قدرت ادامه بده.",
    ];

    /// <summary>
    /// To the member on their first visit after an absence SMS (task 10.20): the text whose turn it is,
    /// starting again after the last.
    /// </summary>
    /// <param name="turn">How many welcome backs the member already has, whatever became of them.</param>
    public static string ForWelcomeBack(string fullName, int turn)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(turn);

        var text = WelcomeBackTexts[turn % WelcomeBackTexts.Count];

        return WithOptOut(string.Format(CultureInfo.InvariantCulture, text, FirstName(fullName)));
    }

    /// <summary>
    /// The broadcast (task 10.13): the member's first name, then the Owner's own text, which the
    /// caller has already trimmed (<c>SmsBroadcast</c>).
    /// </summary>
    public static string ForBroadcast(string fullName, string ownerText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerText);

        return WithOptOut($"{FirstName(fullName)} عزیز، {ownerText}");
    }

    // ---- The values ----

    /// <summary>
    /// The member's first name (BUSINESS_RULES.md §10 <i>Sending</i>, task 10.13): the first word of
    /// the full name, or the word after it when the first is <see cref="SayyedTitle"/>, cut at
    /// <see cref="MaxValueLength"/> characters. A name of one word is used whole, «سید» alone too.
    /// </summary>
    public static string FirstName(string fullName)
    {
        ArgumentNullException.ThrowIfNull(fullName);

        var words = fullName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return string.Empty;
        }

        // Normalized, so «سيد» typed with the Arabic ye is the title too.
        var first = words.Length > 1 && PersianText.Normalize(words[0]) == SayyedTitle ? words[1] : words[0];

        return first.Length <= MaxValueLength ? first : first[..MaxValueLength];
    }

    /// <summary><c>2026-10-19</c> → <c>۰۷/۲۷</c>: the daily report and the receipt leave the year out.</summary>
    public static string MonthAndDay(DateOnly date)
    {
        var (_, month, day) = JalaliCalendar.Parts(date);

        return ToPersianDigits(string.Create(CultureInfo.InvariantCulture, $"{month:D2}/{day:D2}"));
    }

    /// <summary>
    /// An amount in Toman as thousands of Toman, rounded to the nearest thousand, 500 rounded away from
    /// zero: <c>12345600</c> → <c>۱۲٬۳۴۶</c>, <c>-1500</c> → <c>−۲</c>. A figure below zero (more
    /// refunded than taken) carries the app's minus sign, with the left-to-right mark before it so it
    /// stays in front of the number in a right-to-left line, as <c>formatMoney</c> writes it.
    /// </summary>
    public static string Thousands(decimal amount)
    {
        var thousands = decimal.Round(amount / 1000m, MidpointRounding.AwayFromZero);
        var grouped = Math.Abs(thousands).ToString("#,0", CultureInfo.InvariantCulture).Replace(',', ThousandsSeparator);

        return (thousands < 0 ? MinusSign : string.Empty) + ToPersianDigits(grouped);
    }

    /// <summary><c>2026-10-12</c> → <c>۱۴۰۵/۰۷/۲۰</c>.</summary>
    public static string Date(DateOnly date)
    {
        var (year, month, day) = JalaliCalendar.Parts(date);

        return ToPersianDigits(string.Create(CultureInfo.InvariantCulture, $"{year:D4}/{month:D2}/{day:D2}"));
    }

    /// <summary><c>2</c> → <c>۲</c>.</summary>
    public static string Number(int value) => ToPersianDigits(value.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// <c>12500000</c> → <c>۱۲٬۵۰۰٬۰۰۰</c>; a fraction stays as it is, without its trailing zeros:
    /// <c>1500000.50</c> → <c>۱٬۵۰۰٬۰۰۰٫۵</c>. In Toman, like every amount in the app.
    /// </summary>
    public static string Amount(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        var whole = decimal.Truncate(amount);
        var grouped = whole.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', ThousandsSeparator);

        var fraction = (amount - whole).ToString(CultureInfo.InvariantCulture).TrimEnd('0');
        if (fraction.Length > 0 && fraction != "0.")
        {
            // "0.5" → "٫5"
            grouped += DecimalSeparator + fraction[2..];
        }

        return ToPersianDigits(grouped);
    }

    /// <summary>
    /// Shortens a payee: runs of white space become one space, only the first
    /// <paramref name="maxWords"/> words are kept, and what is left is cut at
    /// <see cref="MaxValueLength"/> characters (decided with the developer, 1405/07/14: simply cut).
    /// </summary>
    public static string Fit(string value, int maxWords)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxWords);

        var words = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var kept = string.Join(' ', words.Take(maxWords));

        return kept.Length <= MaxValueLength ? kept : kept[..MaxValueLength].TrimEnd();
    }

    private static string WithOptOut(string text) => $"{text}\n{OptOut}";


    private static string ToPersianDigits(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsAsciiDigit(character) ? (char)('۰' + (character - '0')) : character);
        }

        return builder.ToString();
    }
}
