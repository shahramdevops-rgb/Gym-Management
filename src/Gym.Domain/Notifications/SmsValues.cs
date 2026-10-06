using System.Globalization;
using System.Text;

using Gym.Domain.Common;
using Gym.Domain.Payables;

namespace Gym.Domain.Notifications;

/// <summary>
/// The values for each template's blanks (<c>docs/sms-templates.md</c>), written the way the app
/// shows them: Persian digits, a Jalali date as <c>۱۴۰۵/۰۷/۲۰</c>, an amount with <c>٬</c> between
/// thousands. A date, a number or an amount holds no space, so it fits the blanks that allow none.
/// </summary>
public static class SmsValues
{
    /// <summary>The Persian thousands separator (U+066C), as <c>formatMoney</c> in the frontend writes it.</summary>
    public const char ThousandsSeparator = '٬';

    /// <summary>The Persian decimal separator (U+066B).</summary>
    public const char DecimalSeparator = '٫';

    /// <summary>The «%token» word for a cheque and an instalment in <c>gymPayableDue</c>.</summary>
    public const string ChequeWord = "چک";

    public const string InstallmentWord = "قسط";

    // ---- Each template's blanks (docs/sms-templates.md) ----

    /// <summary><c>gymExpiring</c>: the end date in <c>%token</c>, the name in <c>%token10</c>.</summary>
    public static SmsTokens ForRunningOut(string fullName, DateOnly endDate) =>
        new(Date(endDate), Token10: Name(fullName));

    /// <summary><c>gymLowSessions</c>: the sessions left in <c>%token</c>, the name in <c>%token10</c>.</summary>
    public static SmsTokens ForFewSessionsLeft(string fullName, int sessionsLeft) =>
        new(Number(sessionsLeft), Token10: Name(fullName));

    /// <summary><c>gymBirthday</c> and <c>gymBirthdayEarly</c>: this year's birthday in <c>%token</c>, the name in <c>%token10</c>.</summary>
    public static SmsTokens ForBirthday(string fullName, DateOnly birthday) =>
        new(Date(birthday), Token10: Name(fullName));

    /// <summary><c>gymPayableDue</c>: the kind, the amount, the date and the payee.</summary>
    public static SmsTokens ForPayableDue(PayableKind kind, decimal amount, DateOnly dueDate, string payee)
    {
        ArgumentNullException.ThrowIfNull(payee);

        var word = kind == PayableKind.Cheque ? ChequeWord : InstallmentWord;

        return new(word, Amount(amount), Date(dueDate), Token20: SmsTokens.Fit(payee, SmsTokens.MaxSpacesInToken20));
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

    /// <summary>A long name is simply cut short to what <c>%token10</c> holds (decided with the developer).</summary>
    private static string Name(string fullName)
    {
        ArgumentNullException.ThrowIfNull(fullName);

        return SmsTokens.Fit(fullName, SmsTokens.MaxSpacesInToken10);
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
