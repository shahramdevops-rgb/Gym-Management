using Gym.Domain.Common;

namespace Gym.Domain.Bookings;

/// <summary>
/// A student's session the trainer cancelled (BUSINESS_RULES.md §15 <i>Timetable and attendance</i>,
/// task 15.6c): the trainer saved a change of the timetable that removed it or moved its times while
/// the student was in it. The student is told in the app until they press «دیدم» or the date passes,
/// and by SMS for a near date.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written at the save, not worked out later.</b> «لغو شد» on the student's sessions is read from
/// the timetable as it is now, but whether the student has seen it, and which save to send one SMS
/// for, can only be kept.
/// </para>
/// <para>
/// <b>One per student, save and session</b>, held by a unique index too. A session cancelled, brought
/// back and cancelled again has a row for each save; the app shows it once.
/// </para>
/// </remarks>
public sealed class SessionCancellation : Entity
{
    // For EF Core.
    private SessionCancellation()
    {
    }

    public Guid MemberId { get; private set; }

    public Guid TrainerId { get; private set; }

    /// <summary>The trainer's save: the same for every session it cancelled, for every student.</summary>
    public Guid ChangeId { get; private set; }

    public DateOnly Date { get; private set; }

    public TimeOnly Start { get; private set; }

    public TimeOnly End { get; private set; }

    /// <summary>A moment (UTC): when the student pressed «دیدم»; <c>null</c> until then.</summary>
    public DateTimeOffset? SeenAt { get; private set; }

    /// <remarks>When the trainer saved the change is <see cref="Entity.CreatedAt"/>.</remarks>
    public static SessionCancellation Create(Guid memberId, Guid trainerId, Guid changeId, DateOnly date, TimeOnly start, TimeOnly end)
    {
        CheckId(memberId, nameof(memberId));
        CheckId(trainerId, nameof(trainerId));
        CheckId(changeId, nameof(changeId));
        if (end <= start)
        {
            throw new ArgumentException("A session ends after it starts.", nameof(end));
        }

        return new SessionCancellation
        {
            MemberId = memberId,
            TrainerId = trainerId,
            ChangeId = changeId,
            Date = date,
            Start = start,
            End = end,
        };
    }

    /// <summary>
    /// Whether its SMS is still worth sending: the date is from today to
    /// <paramref name="daysAhead"/> days after it, the session has not started, and the student has
    /// not seen it in the app already.
    /// </summary>
    public bool IsSmsDue(DateOnly today, TimeOnly timeOfDay, int daysAhead) =>
        SeenAt is null
        && Date <= today.AddDays(daysAhead)
        && !BookingRules.HasStarted(Date, Start, today, timeOfDay);

    /// <summary>The student pressed «دیدم». A second press keeps the first moment.</summary>
    public void MarkSeen(DateTimeOffset now) => SeenAt ??= now;

    private static void CheckId(Guid id, string name)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The id must be set.", name);
        }
    }
}
