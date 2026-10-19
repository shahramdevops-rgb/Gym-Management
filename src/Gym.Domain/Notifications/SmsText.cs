using System.Globalization;
using System.Text;

using Gym.Domain.Common;
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
/// <b>A long name or payee is cut short</b> (<see cref="Fit"/>), as it was in the templates' blanks,
/// so one long name cannot make a message cost several parts.
/// </para>
/// <para>
/// <b>Every text ends with <see cref="OptOut"/> on its own line</b> (1405/07/26). Kavenegar adds it
/// to every message from the gym's line anyway; written here, the history keeps the text exactly as
/// it reached the phone.
/// </para>
/// </remarks>
public static class SmsText
{
    /// <summary>The longest text the four can make: a 100-character name or payee and the wording.</summary>
    public const int MaxLength = 500;

    /// <summary>The most characters a name or a payee keeps.</summary>
    public const int MaxValueLength = 100;

    /// <summary>A member's name keeps its first 6 words, as the template's name blank allowed.</summary>
    public const int MaxNameWords = 6;

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

    /// <summary>The subscription running out: the end date, not the days left.</summary>
    public static string ForRunningOut(string fullName, DateOnly endDate) =>
        WithOptOut($"{Name(fullName)} عزیز، اشتراک شما در باشگاه پاسارگاد {Date(endDate)} به پایان می‌رسد.");

    public static string ForFewSessionsLeft(string fullName, int sessionsLeft) =>
        WithOptOut($"{Name(fullName)} عزیز، فقط {Number(sessionsLeft)} جلسه از اشتراک شما در باشگاه پاسارگاد مانده است.");

    /// <summary>
    /// Always sent 1 to 7 days early, so one text: the date without the year, the free visit on it
    /// (BUSINESS_RULES.md §7 <i>Birthday visit</i>) and «پیشاپیش تولدتان مبارک» (chosen by the
    /// developer, 1405/07/27, task 10.8).
    /// </summary>
    /// <param name="birthday">This year's birthday, as <c>SmsAudience.BirthdayToGreet</c> found it.</param>
    public static string ForBirthday(string fullName, DateOnly birthday) =>
        WithOptOut($"{Name(fullName)} عزیز، {MonthAndDay(birthday)} روز تولد شماست؛ آن روز مهمان باشگاه پاسارگاد هستید و ورودتان رایگان است. پیشاپیش تولدتان مبارک!");

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
        WithOptOut($"{Name(fullName)} عزیز، اشتراک {Number(sessions)} جلسه‌ای شما در باشگاه پاسارگاد از {MonthAndDay(startDate)} تا {MonthAndDay(endDate)} ثبت و {Amount(price)} تومان کامل پرداخت شد. سپاس!");

    /// <summary>
    /// The receipt of a plan queued behind another (BUSINESS_RULES.md §4): its dates move if the plan
    /// before it ends early or is frozen, so it gives its length instead of a promise.
    /// </summary>
    public static string ForQueuedSubscriptionReceipt(string fullName, int sessions, int days, decimal price) =>
        WithOptOut($"{Name(fullName)} عزیز، اشتراک {Number(sessions)} جلسه‌ای شما در باشگاه پاسارگاد ({Number(days)} روز، پس از اشتراک فعلی) ثبت و {Amount(price)} تومان کامل پرداخت شد. سپاس!");

    // ---- The values ----

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
    /// Shortens a name or a payee: runs of white space become one space, only the first
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

    private static string Name(string fullName)
    {
        ArgumentNullException.ThrowIfNull(fullName);

        return Fit(fullName, MaxNameWords);
    }

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
