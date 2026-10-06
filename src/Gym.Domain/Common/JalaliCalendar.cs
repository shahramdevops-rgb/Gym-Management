using System.Globalization;

namespace Gym.Domain.Common;

/// <summary>
/// The Jalali (Solar Hijri) calendar for the few rules that follow it on the server: a birthday is
/// kept by the Jalali month and day (BUSINESS_RULES.md §10), and an SMS writes its dates the way the
/// app shows them. Everything stored stays Gregorian (CLAUDE.md); this only reads it.
/// </summary>
/// <remarks>
/// .NET's own <see cref="PersianCalendar"/> does the arithmetic. One instance is shared: its methods
/// read nothing but their arguments.
/// </remarks>
public static class JalaliCalendar
{
    /// <summary>Esfand, the twelfth month: 29 days, 30 in a leap year.</summary>
    public const int Esfand = 12;

    private static readonly PersianCalendar Calendar = new();

    /// <summary>The Jalali year, month and day of <paramref name="date"/>.</summary>
    public static (int Year, int Month, int Day) Parts(DateOnly date)
    {
        var dateTime = date.ToDateTime(TimeOnly.MinValue);

        return (Calendar.GetYear(dateTime), Calendar.GetMonth(dateTime), Calendar.GetDayOfMonth(dateTime));
    }

    /// <summary>The Gregorian date of a Jalali one. A day the month does not have throws: it is a bug in the caller.</summary>
    public static DateOnly ToDate(int year, int month, int day) =>
        DateOnly.FromDateTime(Calendar.ToDateTime(year, month, day, 0, 0, 0, 0));

    /// <summary>A leap year has a 30th of Esfand.</summary>
    public static bool IsLeapYear(int year) => Calendar.IsLeapYear(year);

    /// <summary>
    /// The member's birthday in the Jalali year <paramref name="year"/>: the same month and day, except
    /// that someone born on 30 Esfand has it on 29 Esfand in a year without that day (§10, the same
    /// day the desk celebrates).
    /// </summary>
    public static DateOnly BirthdayIn(DateOnly birthDate, int year)
    {
        var (_, month, day) = Parts(birthDate);
        if (month == Esfand && day == 30 && !IsLeapYear(year))
        {
            day = 29;
        }

        return ToDate(year, month, day);
    }

    /// <summary>The member's next birthday on or after <paramref name="today"/>: this Jalali year's, or next year's once it has passed.</summary>
    public static DateOnly NextBirthday(DateOnly birthDate, DateOnly today)
    {
        var (year, _, _) = Parts(today);
        var thisYear = BirthdayIn(birthDate, year);

        return thisYear >= today ? thisYear : BirthdayIn(birthDate, year + 1);
    }
}
