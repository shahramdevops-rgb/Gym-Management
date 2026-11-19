using Gym.Domain.Common;

namespace Gym.Domain.Coaching;

public static class CoachingErrors
{
    /// <summary>
    /// The member's area is closed: the desk deactivated the member (BUSINESS_RULES.md §15). «me»
    /// already shows the notice; this is what every other member endpoint answers.
    /// </summary>
    public static readonly Error MemberInactive = Error.Forbidden(
        "Coaching.MemberInactive",
        "The membership is inactive.");

    /// <summary>The trainer's area is closed: the Owner stopped the trainer, or the desk deactivated them as a member.</summary>
    public static readonly Error NotATrainer = Error.Forbidden(
        "Coaching.NotATrainer",
        "The account is not a trainer now.");

    /// <summary>No trainer has that id, or the Owner has stopped them: only trainers now can be picked.</summary>
    public static readonly Error TrainerNotFound = Error.NotFound(
        "Coaching.TrainerNotFound",
        "No trainer now has that id.");

    /// <summary>A trainer who is also a member cannot be their own trainer.</summary>
    public static readonly Error CannotPickYourself = Error.BusinessRule(
        "Coaching.CannotPickYourself",
        "A trainer cannot pick themselves.");

    public static readonly Error AlreadyYourTrainer = Error.Conflict(
        "Coaching.AlreadyYourTrainer",
        "That trainer is already the member's trainer.");

    /// <summary>At most <see cref="TrainerRequest.MaxPerDay"/> requests a gym day, so changing back and forth cannot cost many SMS.</summary>
    public static readonly Error TooManyRequestsToday = Error.BusinessRule(
        "Coaching.TooManyRequestsToday",
        "The member has made as many trainer requests today as allowed.");

    public static readonly Error NoPendingRequest = Error.Conflict(
        "Coaching.NoPendingRequest",
        "The member has no request waiting for an answer.");

    public static readonly Error RequestNotFound = Error.NotFound(
        "Coaching.RequestNotFound",
        "The trainer has no request with that id.");

    /// <summary>The request was answered, taken back or replaced meanwhile.</summary>
    public static readonly Error RequestNoLongerPending = Error.Conflict(
        "Coaching.RequestNoLongerPending",
        "The request is no longer waiting for an answer.");

    public static readonly Error NoTrainer = Error.Conflict(
        "Coaching.NoTrainer",
        "The member has no trainer now.");

    public static readonly Error StudentNotFound = Error.NotFound(
        "Coaching.StudentNotFound",
        "That member is not the trainer's student now.");

    public static readonly Error LinkAlreadyEnded = Error.Conflict(
        "Coaching.LinkAlreadyEnded",
        "The link has already ended.");

    /// <summary>
    /// «ویرایش دوره» only while the coaching runs (15.7k): a link with no period types its days left
    /// instead, and a period that is over is renewed.
    /// </summary>
    public static readonly Error PeriodNotRunning = Error.Conflict(
        "Coaching.PeriodNotRunning",
        "The coaching period is not running: it was never given, or it is over.");

    /// <summary>
    /// «درخواست تمدید» (15.7o) only from the period's last days (the SMS settings' number for them)
    /// or after it is over; a link with no period has none.
    /// </summary>
    public static readonly Error RenewalNotYet = Error.BusinessRule(
        "Coaching.RenewalNotYet",
        "A renewal can be asked for only in the coaching period's last days, or after it is over.");

    public static readonly Error NoRenewalRequest = Error.Conflict(
        "Coaching.NoRenewalRequest",
        "The member has no renewal request waiting for an answer.");

    /// <summary>
    /// No renewal request of the trainer's students has that id, or its member was deactivated at the
    /// desk since asking: it waits until the member is active again.
    /// </summary>
    public static readonly Error RenewalRequestNotFound = Error.NotFound(
        "Coaching.RenewalRequestNotFound",
        "The trainer has no renewal request with that id.");

    /// <summary>The renewal request was answered, taken back, or closed by the coaching's end meanwhile.</summary>
    public static readonly Error RenewalNoLongerPending = Error.Conflict(
        "Coaching.RenewalNoLongerPending",
        "The renewal request is no longer waiting for an answer.");

    /// <summary>
    /// A coaching period is <see cref="CoachingPeriod.MinDays"/> to <see cref="CoachingPeriod.MaxDays"/>
    /// days; the days left of a link made before 15.7h, from <see cref="CoachingPeriod.MinDaysLeft"/>.
    /// </summary>
    public static readonly Error PeriodDaysOutOfRange = Error.Validation(
        "Coaching.PeriodDaysOutOfRange",
        $"A coaching period is {CoachingPeriod.MinDays} to {CoachingPeriod.MaxDays} days; the days left of an earlier link, from {CoachingPeriod.MinDaysLeft}.");
}
