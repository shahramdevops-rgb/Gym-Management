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

    // ---- The four kinds (docs/sms-texts.md) ----

    /// <summary>The subscription running out: the end date, not the days left.</summary>
    public static string ForRunningOut(string fullName, DateOnly endDate) =>
        $"{Name(fullName)} عزیز، اشتراک شما در باشگاه پاسارگاد {Date(endDate)} به پایان می‌رسد.";

    public static string ForFewSessionsLeft(string fullName, int sessionsLeft) =>
        $"{Name(fullName)} عزیز، فقط {Number(sessionsLeft)} جلسه از اشتراک شما در باشگاه پاسارگاد مانده است.";

    /// <summary>
    /// «تولدتان مبارک» on the day itself, «پیشاپیش» when the greeting goes out 1 to 7 days early:
    /// picked by the date, so it always matches the day the message is written.
    /// </summary>
    /// <param name="birthday">This year's birthday, as <c>SmsAudience.BirthdayToGreet</c> found it.</param>
    /// <param name="today">The gym's date the message is written on.</param>
    public static string ForBirthday(string fullName, DateOnly birthday, DateOnly today) =>
        birthday == today
            ? $"{Name(fullName)} عزیز، امروز {Date(birthday)} روز تولد شماست. تولدتان مبارک! باشگاه پاسارگاد"
            : $"{Name(fullName)} عزیز، تولدتان در {Date(birthday)} را پیشاپیش تبریک می‌گوییم. باشگاه پاسارگاد";

    /// <summary>To the Owner: the kind, the amount, the date and the payee.</summary>
    public static string ForPayableDue(PayableKind kind, decimal amount, DateOnly dueDate, string payee)
    {
        ArgumentNullException.ThrowIfNull(payee);

        var word = kind == PayableKind.Cheque ? ChequeWord : InstallmentWord;

        return $"یادآوری {word}: {Amount(amount)} تومان، سررسید {Date(dueDate)}، به {Fit(payee, MaxPayeeWords)}";
    }

    // ---- The values ----

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
