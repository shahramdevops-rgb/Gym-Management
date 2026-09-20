using Gym.Domain.Common;

namespace Gym.Domain.Attendances;

/// <summary>
/// One visit: a member checked in, consuming a session from a subscription and optionally taking
/// a locker (BUSINESS_RULES.md §7).
/// </summary>
/// <remarks>
/// Every precondition (member active, subscription active, no open attendance) is a cross-entity
/// rule Domain cannot check itself, so <see cref="CheckIn"/> takes an already-validated
/// <paramref name="subscriptionId"/> and trusts the caller, the same way
/// <c>SubscriptionSeller</c> decides which plan and member before calling
/// <c>Subscription.Create</c>.
/// </remarks>
public sealed class Attendance : Entity
{
    // For EF Core.
    private Attendance()
    {
    }

    public Guid MemberId { get; private set; }

    public Guid SubscriptionId { get; private set; }

    /// <summary><c>null</c> when no locker was free at check-in (BUSINESS_RULES.md §7).</summary>
    public Guid? LockerId { get; private set; }

    /// <summary>A moment (UTC), not a business date: it records when the member arrived.</summary>
    public DateTimeOffset CheckedInAt { get; private set; }

    /// <summary>
    /// <c>null</c> means still open. Set by <see cref="CheckOut"/> and <see cref="Cancel"/> — the
    /// partial unique indexes on <see cref="MemberId"/> and <see cref="LockerId"/> (one open
    /// attendance per member, one per locker) both filter on it, so a cancelled attendance counts
    /// as closed the same as a checked-out one.
    /// </summary>
    public DateTimeOffset? CheckedOutAt { get; private set; }

    /// <summary><c>null</c> unless <see cref="Cancel"/> closed this attendance (BUSINESS_RULES.md §7).</summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    public static Attendance CheckIn(Guid memberId, Guid subscriptionId, Guid? lockerId, DateTimeOffset checkedInAt) =>
        new()
        {
            MemberId = memberId,
            SubscriptionId = subscriptionId,
            LockerId = lockerId,
            CheckedInAt = checkedInAt,
        };

    /// <summary>Only an open attendance can be checked out (BUSINESS_RULES.md §7).</summary>
    public Result CheckOut(DateTimeOffset checkedOutAt)
    {
        if (CheckedOutAt is not null)
        {
            return Result.Failure(AttendanceErrors.NotOpen);
        }

        CheckedOutAt = checkedOutAt;

        return Result.Success();
    }

    /// <summary>
    /// Cancels an open attendance within the allowed window of check-in (BUSINESS_RULES.md §7).
    /// Restoring the session is the caller's job (it belongs to the subscription, a different
    /// aggregate); this only records the cancellation and frees the locker.
    /// </summary>
    /// <param name="cancelWindowMinutes"><c>Gym:CancelCheckInWindowMinutes</c>.</param>
    public Result Cancel(DateTimeOffset now, int cancelWindowMinutes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cancelWindowMinutes);

        if (CheckedOutAt is not null)
        {
            return Result.Failure(AttendanceErrors.NotOpen);
        }

        if (now - CheckedInAt > TimeSpan.FromMinutes(cancelWindowMinutes))
        {
            return Result.Failure(AttendanceErrors.CancelWindowExpired);
        }

        CancelledAt = now;
        CheckedOutAt = now;

        return Result.Success();
    }
}
