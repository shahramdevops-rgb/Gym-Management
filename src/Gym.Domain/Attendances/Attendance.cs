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
    /// <c>null</c> means still open. Task 5.3 adds check-out and cancel, the only ways this
    /// becomes non-null; the column exists from this task because the partial unique indexes on
    /// <see cref="MemberId"/> and <see cref="LockerId"/> (one open attendance per member, one per
    /// locker) both filter on it.
    /// </summary>
    public DateTimeOffset? CheckedOutAt { get; private set; }

    public static Attendance CheckIn(Guid memberId, Guid subscriptionId, Guid? lockerId, DateTimeOffset checkedInAt) =>
        new()
        {
            MemberId = memberId,
            SubscriptionId = subscriptionId,
            LockerId = lockerId,
            CheckedInAt = checkedInAt,
        };
}
