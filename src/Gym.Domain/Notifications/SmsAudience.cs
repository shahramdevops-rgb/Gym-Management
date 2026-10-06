using Gym.Domain.Common;
using Gym.Domain.Payables;
using Gym.Domain.Subscriptions;

namespace Gym.Domain.Notifications;

/// <summary>
/// Who is due which SMS today (BUSINESS_RULES.md §10 <i>The four kinds</i>), one rule per kind.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rules, not the search.</b> The daily run asks the database for the likely ones and then
/// asks these methods, so each rule is written, and unit tested, once. Two conditions need other
/// rows and stay in the run's query: the member is not deactivated, and has not already bought the
/// next subscription.
/// </para>
/// <para>
/// <b>Each is a window, not a single day.</b> "At most N days away, today included" catches someone
/// a missed run left out, a subscription sold a few days before its end, and the people a larger
/// number brings in. The unique indexes keep that from sending anything twice.
/// </para>
/// </remarks>
public static class SmsAudience
{
    /// <summary>
    /// An active subscription (not frozen, not queued, not used up) ending within
    /// <paramref name="daysBefore"/> days, today included. A single visit never gets one.
    /// </summary>
    public static bool IsRunningOut(Subscription subscription, DateOnly today, int daysBefore)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return IsActivePlan(subscription, today)
            && subscription.EndDate.DayNumber - today.DayNumber <= daysBefore;
    }

    /// <summary>An active subscription with at most <paramref name="threshold"/> sessions left. A single visit never gets one.</summary>
    public static bool HasFewSessionsLeft(Subscription subscription, DateOnly today, int threshold)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return IsActivePlan(subscription, today) && subscription.RemainingSessions <= threshold;
    }

    /// <summary>
    /// The birthday to greet today, or <c>null</c>. With <paramref name="daysBefore"/> = 0, the
    /// birthday itself; otherwise a birthday from tomorrow to <paramref name="daysBefore"/> days ahead,
    /// so the «پیشاپیش» greeting never lands on the day (decided with the developer, task 10.3). The
    /// birthday's Jalali year is the year of the message: one per member and year.
    /// </summary>
    public static DateOnly? BirthdayToGreet(DateOnly birthDate, DateOnly today, int daysBefore)
    {
        var birthday = JalaliCalendar.NextBirthday(birthDate, today);

        // The day someone was born is not a birthday: the first one is a year later.
        if (birthday <= birthDate)
        {
            return null;
        }

        var daysAway = birthday.DayNumber - today.DayNumber;
        var inWindow = daysBefore == 0 ? daysAway == 0 : daysAway >= 1 && daysAway <= daysBefore;

        return inWindow ? birthday : null;
    }

    /// <summary>
    /// A pending cheque or instalment dated from today to <paramref name="daysBefore"/> days ahead.
    /// One past its date is left to the header alert: a reminder after the day reminds of nothing.
    /// </summary>
    public static bool IsComingDue(Payable payable, DateOnly today, int daysBefore)
    {
        ArgumentNullException.ThrowIfNull(payable);

        var daysAway = payable.DueDate.DayNumber - today.DayNumber;

        return payable.PaidAt is null
            && payable.CancelledAt is null
            && daysAway >= 0
            && daysAway <= daysBefore;
    }

    private static bool IsActivePlan(Subscription subscription, DateOnly today) =>
        !subscription.IsSingleSession && subscription.GetStatus(today) == SubscriptionStatus.Active;
}
