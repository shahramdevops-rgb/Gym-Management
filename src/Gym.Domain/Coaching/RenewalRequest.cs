using Gym.Domain.Common;

namespace Gym.Domain.Coaching;

/// <summary>
/// A student asking their trainer to renew the coaching period (درخواست تمدید, BUSINESS_RULES.md §15
/// <i>The coaching period</i>, task 15.7o): from the period's last <i>N</i> days (the SMS settings'
/// number for them) and after it is over, while the coaching lasts. The trainer renews or declines;
/// until then the student may take it back. No money is involved: the student pays as before.
/// </summary>
/// <remarks>
/// <para>
/// <b>One waiting per link</b>, held by a partial unique index too. A request never comes back to
/// <see cref="RenewalRequestStatus.Pending"/>: asking again is a new request.
/// </para>
/// <para>
/// <b>It keeps the last day it was asked about</b> (<see cref="PeriodEndsOn"/>): a decline is shown
/// to the student only while the coaching still ends on that day, so a «تمدید» or «ویرایش» made after
/// it hides it (decided with 15.7o's plan, 1405/08/28). Kept, never deleted.
/// </para>
/// </remarks>
public sealed class RenewalRequest : Entity
{
    // For EF Core.
    private RenewalRequest()
    {
    }

    /// <summary>The trainer and student's link whose period is to be renewed.</summary>
    public Guid LinkId { get; private set; }

    /// <summary>The gym's day it was asked on.</summary>
    public DateOnly RequestedOn { get; private set; }

    /// <summary>The coaching's last day when it was asked.</summary>
    public DateOnly PeriodEndsOn { get; private set; }

    public RenewalRequestStatus Status { get; private set; }

    /// <summary>
    /// The gym's day the trainer renewed or declined it; <c>null</c> while it waits and for every
    /// other outcome.
    /// </summary>
    public DateOnly? AnsweredOn { get; private set; }

    /// <summary>Postgres <c>xmin</c>: the second of two changes at once fails instead of overwriting.</summary>
    public uint Version { get; private set; }

    public bool IsPending => Status == RenewalRequestStatus.Pending;

    /// <summary>
    /// Whether a coaching ending on <paramref name="periodEndsOn"/> may be asked to be renewed today:
    /// it has at most <paramref name="soonDays"/> days left, its last day counting as one, or it is
    /// over. A link with no period (made before 15.7h) may not.
    /// </summary>
    /// <param name="soonDays">The SMS settings' number for the period's last days (15.7n).</param>
    public static bool CanAsk(DateOnly? periodEndsOn, DateOnly today, int soonDays) =>
        periodEndsOn is { } lastDay && lastDay.DayNumber - today.DayNumber + 1 <= soonDays;

    /// <summary>The student asks to renew <paramref name="link"/>'s coaching. Needs its periods loaded.</summary>
    /// <param name="today">The gym's today.</param>
    /// <param name="soonDays">The SMS settings' number for the period's last days (15.7n).</param>
    public static Result<RenewalRequest> Create(TrainerLink link, DateOnly today, int soonDays)
    {
        ArgumentNullException.ThrowIfNull(link);

        if (!link.IsOpen)
        {
            return Result.Failure<RenewalRequest>(CoachingErrors.LinkAlreadyEnded);
        }

        if (link.PeriodEndsOn is not { } lastDay || !CanAsk(lastDay, today, soonDays))
        {
            return Result.Failure<RenewalRequest>(CoachingErrors.RenewalNotYet);
        }

        return new RenewalRequest
        {
            LinkId = link.Id,
            RequestedOn = today,
            PeriodEndsOn = lastDay,
            Status = RenewalRequestStatus.Pending,
        };
    }

    /// <summary>
    /// Whether the student still reads «درخواست تمدید رد شد»: declined, and the coaching still ends on
    /// the day it was asked about.
    /// </summary>
    public bool ShowsDecline(DateOnly? periodEndsOnNow) =>
        Status == RenewalRequestStatus.Declined && PeriodEndsOn == periodEndsOnNow;

    /// <summary>
    /// The trainer renewed the coaching: from «درخواست‌ها», on «برنامهٔ ثابت», or with a «ویرایش» that
    /// moved the last day later. Renewing the link is the caller's.
    /// </summary>
    /// <param name="today">The gym's today.</param>
    public Result Renew(DateOnly today) => Answer(RenewalRequestStatus.Renewed, today);

    /// <param name="today">The gym's today.</param>
    public Result Decline(DateOnly today) => Answer(RenewalRequestStatus.Declined, today);

    /// <summary>The student takes it back («لغو درخواست»).</summary>
    public Result Cancel() => Close(RenewalRequestStatus.Cancelled);

    /// <summary>The coaching ended while it was waiting (BUSINESS_RULES.md §15: ending the coaching closes it).</summary>
    public Result CloseForEndedLink() => Close(RenewalRequestStatus.LinkEnded);

    private Result Answer(RenewalRequestStatus answer, DateOnly today)
    {
        var closed = Close(answer);
        if (closed.IsSuccess)
        {
            // A clock that went back would answer it before it was asked; the database refuses that too.
            AnsweredOn = today < RequestedOn ? RequestedOn : today;
        }

        return closed;
    }

    private Result Close(RenewalRequestStatus outcome)
    {
        if (!IsPending)
        {
            return Result.Failure(CoachingErrors.RenewalNoLongerPending);
        }

        Status = outcome;

        return Result.Success();
    }
}
