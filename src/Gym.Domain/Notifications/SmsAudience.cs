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
    /// The birthday to greet today, or <c>null</c>: a birthday from tomorrow to
    /// <paramref name="daysBefore"/> days ahead, never the day itself, so the member hears of the free
    /// visit in time (task 10.8; the settings allow 1 to 7). The birthday's Jalali year is the year of
    /// the message: one per member and year.
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

        return daysAway >= 1 && daysAway <= daysBefore ? birthday : null;
    }

    /// <summary>
    /// A plan that earns the birthday SMS (§10, task 10.8): a membership, not a single visit, that is
    /// active, frozen or bought for later on the day the message is written. One used up, ended or
    /// cancelled does not count. Whether the member is active is the run's question.
    /// </summary>
    public static bool HoldsPlanForBirthday(Subscription subscription, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return !subscription.IsSingleSession
            && subscription.GetStatus(today) is SubscriptionStatus.Active or SubscriptionStatus.Frozen or SubscriptionStatus.Upcoming;
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

    /// <summary>
    /// Whether a payment earns the receipt (§10 <i>The subscription receipt</i>, task 10.9): it is
    /// the one that clears what was still owed on a plan, so its net paid reaches the price. A
    /// single visit and a free plan never earn one, and a payment on a plan already paid cannot
    /// exist (§5: no overpayment).
    /// </summary>
    /// <param name="owedBefore">What was still owed on it just before this payment.</param>
    /// <param name="amount">This payment.</param>
    public static bool EarnsReceipt(Subscription subscription, decimal owedBefore, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return !subscription.IsSingleSession
            && subscription.Price > 0m
            && owedBefore > 0m
            && amount >= owedBefore;
    }

    /// <summary>
    /// Whether a receipt asked for earlier is still to be sent: the plan is not cancelled and still
    /// fully paid. Whether the member is active is the sender's question.
    /// </summary>
    public static bool ReceiptStillDue(Subscription subscription, decimal netPaid)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return !subscription.IsSingleSession
            && subscription.CancelledAt is null
            && subscription.Price > 0m
            && netPaid >= subscription.Price;
    }

    /// <summary>
    /// Whether a plan is the member's first (§10 <i>The welcome</i>, task 10.10): a membership, not
    /// cancelled, sold while the member had no other membership that is not cancelled. Only the
    /// plans sold before it count: a plan sold after the first does not make it any less the first.
    /// Single visits never count, either way.
    /// </summary>
    /// <remarks>
    /// "Sold before" is the moment it was saved, and the id when two share a moment, so of two plans
    /// exactly one is the first, even when both are asked about after both were saved.
    /// </remarks>
    /// <param name="others">The member's other subscriptions; the plan itself may be among them.</param>
    public static bool IsFirstPlan(Subscription plan, IEnumerable<Subscription> others)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(others);

        if (plan.IsSingleSession || plan.CancelledAt is not null)
        {
            return false;
        }

        return !others.Any(other =>
            other.Id != plan.Id
            && !other.IsSingleSession
            && other.CancelledAt is null
            && SoldBefore(other, plan));
    }

    /// <summary>
    /// A plan waiting in line behind another membership of the same member (§4): it has not started
    /// and a live membership ends the day before it starts, so its dates are only where it stands
    /// today. The same test as the member's history («بعد از پلن قبلی»).
    /// </summary>
    /// <param name="others">The member's other subscriptions; the plan itself may be among them.</param>
    public static bool IsQueuedBehindAnother(Subscription subscription, IEnumerable<Subscription> others, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        ArgumentNullException.ThrowIfNull(others);

        if (subscription.IsSingleSession || subscription.GetStatus(today) != SubscriptionStatus.Upcoming)
        {
            return false;
        }

        var dayBefore = subscription.StartDate.AddDays(-1);

        return others.Any(other =>
            other.Id != subscription.Id
            && !other.IsSingleSession
            && other.CancelledAt is null
            && other.EndDate == dayBefore);
    }

    private static bool SoldBefore(Subscription one, Subscription other) =>
        one.CreatedAt < other.CreatedAt
        || (one.CreatedAt == other.CreatedAt && one.Id.CompareTo(other.Id) < 0);

    private static bool IsActivePlan(Subscription subscription, DateOnly today) =>
        !subscription.IsSingleSession && subscription.GetStatus(today) == SubscriptionStatus.Active;
}
