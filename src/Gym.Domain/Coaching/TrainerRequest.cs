using Gym.Domain.Common;

namespace Gym.Domain.Coaching;

/// <summary>
/// A member asking a trainer to be their trainer, on the site (BUSINESS_RULES.md §15 <i>A member and
/// their trainer</i>). The trainer accepts or declines; until then the member may take it back or
/// pick someone else, which replaces it.
/// </summary>
/// <remarks>
/// <para>
/// <b>One waiting at a time.</b> A member has at most one <see cref="TrainerRequestStatus.Pending"/>
/// request, held by a partial unique index too. A request never comes back to
/// <see cref="TrainerRequestStatus.Pending"/>: asking again is a new request.
/// </para>
/// <para>
/// <b>Kept, never deleted</b>, so the day's count (<see cref="MaxPerDay"/>) and the history of who
/// asked whom stay true.
/// </para>
/// </remarks>
public sealed class TrainerRequest : Entity
{
    /// <summary>
    /// Requests a member may make in one gym day, whatever became of them: each may be an SMS to a
    /// trainer, so changing back and forth is limited (decided by the developer, 1405/08/07, 15.4).
    /// </summary>
    public const int MaxPerDay = 5;

    // For EF Core.
    private TrainerRequest()
    {
    }

    public Guid MemberId { get; private set; }

    public Guid TrainerId { get; private set; }

    /// <summary>The gym's day it was made on, for <see cref="MaxPerDay"/>.</summary>
    public DateOnly RequestedOn { get; private set; }

    public TrainerRequestStatus Status { get; private set; }

    /// <summary>
    /// The gym's day the trainer accepted or declined it (task 15.4d): the member is told in the app
    /// from then on, for <see cref="CoachingNews.ShownForDays"/> days at most. <c>null</c> for every
    /// other outcome, and for the answers given before 15.4d, which nobody is told of.
    /// </summary>
    public DateOnly? AnsweredOn { get; private set; }

    /// <summary>A moment (UTC): when the member pressed «دیدم» on the answer; <c>null</c> until then.</summary>
    public DateTimeOffset? AnswerSeenAt { get; private set; }

    /// <summary>Postgres <c>xmin</c>: the second of two changes at once fails instead of overwriting.</summary>
    public uint Version { get; private set; }

    public bool IsPending => Status == TrainerRequestStatus.Pending;

    /// <param name="today">The gym's today.</param>
    /// <param name="madeToday">The member's requests already made today, whatever became of them.</param>
    public static Result<TrainerRequest> Create(Guid memberId, Guid trainerId, DateOnly today, int madeToday)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(madeToday);

        if (madeToday >= MaxPerDay)
        {
            return Result.Failure<TrainerRequest>(CoachingErrors.TooManyRequestsToday);
        }

        return new TrainerRequest
        {
            MemberId = memberId,
            TrainerId = trainerId,
            RequestedOn = today,
            Status = TrainerRequestStatus.Pending,
        };
    }

    /// <summary>
    /// The trainer accepts: the member's link to them starts today, with a coaching period of
    /// <paramref name="days"/> days (15.7h). Ending the member's earlier link is the caller's, since
    /// that link is another row.
    /// </summary>
    /// <param name="days">Typed by the trainer, <see cref="CoachingPeriod.MinDays"/> to <see cref="CoachingPeriod.MaxDays"/>.</param>
    public Result<TrainerLink> Accept(DateOnly today, int days = CoachingPeriod.DefaultDays)
    {
        // Checked first: a wrong number leaves the request waiting.
        if (!CoachingPeriod.IsAllowed(days, CoachingPeriod.MinDays))
        {
            return Result.Failure<TrainerLink>(CoachingErrors.PeriodDaysOutOfRange);
        }

        var closed = Close(TrainerRequestStatus.Accepted);
        if (closed.IsFailure)
        {
            return Result.Failure<TrainerLink>(closed.Error);
        }

        AnsweredOn = today;

        return TrainerLink.Start(MemberId, TrainerId, Id, today, days);
    }

    /// <param name="today">The gym's today: the day the member is told of it.</param>
    public Result Decline(DateOnly today)
    {
        var closed = Close(TrainerRequestStatus.Declined);
        if (closed.IsSuccess)
        {
            AnsweredOn = today;
        }

        return closed;
    }

    /// <summary>The member takes it back.</summary>
    public Result Cancel() => Close(TrainerRequestStatus.Cancelled);

    /// <summary>The member picked another trainer while this one was waiting.</summary>
    public Result Replace() => Close(TrainerRequestStatus.Replaced);

    /// <summary>The Owner stopped the trainer while this was waiting for them.</summary>
    public Result EndForStoppedTrainer() => Close(TrainerRequestStatus.TrainerStopped);

    /// <summary>
    /// The member pressed «دیدم» on the trainer's answer (task 15.4d). A second press keeps the first
    /// moment.
    /// </summary>
    /// <exception cref="InvalidOperationException">The request has no answer to see.</exception>
    public void MarkAnswerSeen(DateTimeOffset now)
    {
        if (AnsweredOn is null)
        {
            throw new InvalidOperationException("Only an accepted or declined request has an answer to see.");
        }

        AnswerSeenAt ??= now;
    }

    private Result Close(TrainerRequestStatus outcome)
    {
        if (!IsPending)
        {
            return Result.Failure(CoachingErrors.RequestNoLongerPending);
        }

        Status = outcome;

        return Result.Success();
    }
}
