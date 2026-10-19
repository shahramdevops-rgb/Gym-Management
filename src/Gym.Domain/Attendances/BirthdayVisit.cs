using Gym.Domain.Common;
using Gym.Domain.Subscriptions;

namespace Gym.Domain.Attendances;

/// <summary>
/// A member's first visit on their birthday uses no session of their plan (BUSINESS_RULES.md §7
/// <i>Birthday visit</i>, task 10.8). The birthday is the Jalali month and day, the same day the desk
/// celebrates and the birthday SMS names.
/// </summary>
/// <remarks>
/// Whether the member already had their birthday visit this year is a question about other
/// attendances, so the caller asks it; the partial unique index on the attendance's
/// <see cref="Attendance.BirthdayYear"/> holds it too.
/// </remarks>
public static class BirthdayVisit
{
    /// <summary>
    /// The Jalali year of the birthday that makes a visit on <paramref name="subscription"/> today
    /// free, or <c>null</c> when today is not the member's birthday or the subscription is a single
    /// visit, which is used as usual (its money is already paid).
    /// </summary>
    public static int? FreeYear(DateOnly birthDate, Subscription subscription, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        if (subscription.IsSingleSession || !JalaliCalendar.IsBirthday(birthDate, today))
        {
            return null;
        }

        var (year, _, _) = JalaliCalendar.Parts(today);

        return year;
    }
}
