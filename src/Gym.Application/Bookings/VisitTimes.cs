using Gym.Domain.Bookings;

namespace Gym.Application.Bookings;

/// <summary>
/// A check-in at the desk and its check-out, in the gym's time zone, as the trainer reads it
/// (BUSINESS_RULES.md §15 <i>More for the trainer</i>, task 15.7d).
/// </summary>
/// <param name="CheckedOutAt"><c>null</c> while the visit is open («داخل است») or when auto-closed.</param>
/// <param name="AutoCheckedOut">The nightly auto-checkout closed it: «خروج ثبت نشد».</param>
public sealed record VisitTimes(TimeOnly CheckedInAt, TimeOnly? CheckedOutAt, bool AutoCheckedOut)
{
    public static VisitTimes Of(DayVisit visit)
    {
        ArgumentNullException.ThrowIfNull(visit);

        return new VisitTimes(visit.CheckedInAt, visit.CheckedOutAt, visit.AutoClosed);
    }
}
