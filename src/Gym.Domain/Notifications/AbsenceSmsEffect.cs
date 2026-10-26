namespace Gym.Domain.Notifications;

/// <summary>
/// Whether a member came again after their absence SMS (BUSINESS_RULES.md §12 <i>The absence SMS's
/// effect</i>, task 10.19). It says who came after the message, not who came because of it.
/// </summary>
public static class AbsenceSmsEffect
{
    /// <summary>
    /// A member who checks in no later than this many days after the day of the message came back
    /// (made 5 by the developer, 1405/08/04, after 14 and 7: the closer to the message, the more a
    /// visit is its answer).
    /// </summary>
    public const int WindowDays = 5;

    /// <summary>
    /// An absence SMS counts once the provider accepted it, unless it gave the cost back: a message
    /// cancelled or blocked by the receiver never reached the phone (§10 <i>Sending</i>). One whose
    /// delivery is not reported yet counts.
    /// </summary>
    public static bool Counts(NotificationStatus status, SmsDelivery? delivery) =>
        status == NotificationStatus.Sent && !(delivery is { } reported && Notification.IsRefunded(reported));

    /// <param name="sentOn">The gym's day the message was sent: day 0.</param>
    /// <param name="planEndOn">
    /// The end of the plan the member held on <paramref name="sentOn"/>, as it stands now; <c>null</c>
    /// when no such plan is found any more (cancelled since).
    /// </param>
    /// <param name="frozenSince">The day that plan's freeze began, when it is frozen now.</param>
    /// <param name="firstVisitOn">
    /// The gym's day of the member's first check-in after the moment it was sent, not cancelled
    /// (cardio-only counts); <c>null</c> for none.
    /// </param>
    /// <param name="today">The gym's day today.</param>
    public static AbsenceSmsFollowUp FollowUp(
        DateOnly sentOn, DateOnly? planEndOn, DateOnly? frozenSince, DateOnly? firstVisitOn, DateOnly today)
    {
        if (firstVisitOn < sentOn)
        {
            throw new ArgumentOutOfRangeException(nameof(firstVisitOn), firstVisitOn, "A visit before the message is not its follow-up.");
        }

        var windowEnd = sentOn.AddDays(WindowDays);
        var visitInWindow = firstVisitOn is { } visit && visit <= windowEnd ? visit : (DateOnly?)null;

        // Known from the day of sending, whatever the member did: a plan that ends inside the window
        // gave no full chance to come, so it pulls the figure neither way.
        if (planEndOn is not { } planEnd || planEnd < windowEnd)
        {
            return NotMeasurable;
        }

        // Frozen inside the window before coming: away on purpose.
        if (frozenSince is { } frozen && frozen <= windowEnd && (visitInWindow is null || frozen <= visitInWindow))
        {
            return NotMeasurable;
        }

        if (visitInWindow is { } cameOn)
        {
            return new AbsenceSmsFollowUp(AbsenceSmsOutcome.CameBack, cameOn.DayNumber - sentOn.DayNumber, null);
        }

        return today <= windowEnd
            ? new AbsenceSmsFollowUp(AbsenceSmsOutcome.Waiting, null, windowEnd.DayNumber - today.DayNumber)
            : new AbsenceSmsFollowUp(AbsenceSmsOutcome.DidNotComeBack, null, null);
    }

    private static AbsenceSmsFollowUp NotMeasurable => new(AbsenceSmsOutcome.NotMeasurable, null, null);
}

/// <summary>What became of one absence SMS.</summary>
/// <param name="DaysToComeBack">Set when the member came back: days from the message's day to the visit's, 0 on the same day.</param>
/// <param name="DaysLeft">Set while waiting: the days still left in the window, 0 on its last day.</param>
public sealed record AbsenceSmsFollowUp(AbsenceSmsOutcome Outcome, int? DaysToComeBack, int? DaysLeft);

/// <summary>In the order the dashboard's list shows them.</summary>
public enum AbsenceSmsOutcome
{
    CameBack,

    Waiting,

    DidNotComeBack,

    /// <summary>The plan ended inside the window, was cancelled, or was frozen before the member came.</summary>
    NotMeasurable,
}
