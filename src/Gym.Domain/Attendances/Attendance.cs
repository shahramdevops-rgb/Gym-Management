using Gym.Domain.Common;
using Gym.Domain.Members;

namespace Gym.Domain.Attendances;

/// <summary>
/// One visit: a member checked in, consuming a session from a subscription and holding either the
/// locker the desk chose or, when every locker is full, a reserve place (BUSINESS_RULES.md §6, §7).
/// A guest's visit (§7 <i>Guest visit</i>) holds a place the same way, but has a typed name instead
/// of a member and a subscription.
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
    /// <summary>How many visits can be inside with no locker at once (BUSINESS_RULES.md §6 <i>Reserve places</i>).</summary>
    public const int ReservePlaceCount = 15;

    /// <summary>A guest's name follows a member's limit (BUSINESS_RULES.md §7 <i>Guest visit</i>).</summary>
    public const int GuestNameMaxLength = Member.FullNameMaxLength;

    // For EF Core.
    private Attendance()
    {
    }

    /// <summary><c>null</c> only on a guest's visit, which has <see cref="GuestName"/> instead (a check constraint says so too).</summary>
    public Guid? MemberId { get; private set; }

    /// <summary>The subscription a session was consumed from; <c>null</c> exactly when <see cref="MemberId"/> is.</summary>
    public Guid? SubscriptionId { get; private set; }

    /// <summary>
    /// The full name the desk typed for a guest (BUSINESS_RULES.md §7 <i>Guest visit</i>), trimmed
    /// and otherwise as typed, like <see cref="Member.FullName"/>. <c>null</c> on a member's visit.
    /// A guest is never searched for on the server, so there is no normalized copy.
    /// </summary>
    public string? GuestName { get; private set; }

    /// <summary>A guest's visit: no member, no subscription, only a name.</summary>
    public bool IsGuest => MemberId is null;

    /// <summary>
    /// The locker this visit holds, or <c>null</c> when it holds a reserve place instead. An open
    /// visit always holds exactly one of the two (a check constraint says so too). Visits closed
    /// before roadmap 6.5.5 may hold neither: then no locker was free and no reserve place existed.
    /// </summary>
    public Guid? LockerId { get; private set; }

    /// <summary>
    /// The reserve place, 1 to <see cref="ReservePlaceCount"/>, or <c>null</c> when the visit holds a
    /// locker (BUSINESS_RULES.md §6). Internal bookkeeping for the partial unique index that stops two
    /// open visits sharing one; the number is never shown.
    /// </summary>
    public int? ReserveSlot { get; private set; }

    /// <summary>
    /// Whether this visit holds a reserve place, said once here so no screen has to read it off a
    /// missing locker (which a visit closed before 6.5.5 also has).
    /// </summary>
    public bool UsesReservePlace => ReserveSlot is not null;

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

    /// <summary>
    /// <c>null</c> unless the nightly job (BUSINESS_RULES.md §7 Auto-checkout) closed this
    /// attendance instead of the member checking out themselves.
    /// </summary>
    public DateTimeOffset? AutoClosedAt { get; private set; }

    /// <summary>
    /// Postgres <c>xmin</c>. Moving a visit and closing it can race (two desks, or the nightly job);
    /// without this, a move could land on a visit that was closed a moment earlier.
    /// </summary>
    public uint Version { get; private set; }

    /// <summary>A visit with the locker the desk chose (BUSINESS_RULES.md §7, step 3).</summary>
    public static Attendance CheckIn(Guid memberId, Guid subscriptionId, Guid lockerId, DateTimeOffset checkedInAt) =>
        new()
        {
            MemberId = memberId,
            SubscriptionId = subscriptionId,
            LockerId = lockerId,
            CheckedInAt = checkedInAt,
        };

    /// <summary>
    /// A visit on a reserve place, for the day every locker is full (BUSINESS_RULES.md §6). Whether
    /// one may be used, and which one is free, are questions about other visits, so the caller
    /// answers them; a number outside 1–<see cref="ReservePlaceCount"/> is a bug, not a business
    /// failure.
    /// </summary>
    public static Attendance CheckInOnReservePlace(Guid memberId, Guid subscriptionId, int reserveSlot, DateTimeOffset checkedInAt)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(reserveSlot, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(reserveSlot, ReservePlaceCount);

        return new Attendance
        {
            MemberId = memberId,
            SubscriptionId = subscriptionId,
            ReserveSlot = reserveSlot,
            CheckedInAt = checkedInAt,
        };
    }

    /// <summary>
    /// A guest's visit on the locker the desk chose (BUSINESS_RULES.md §7 <i>Guest visit</i>):
    /// no member, no subscription, no session consumed. The locker's checks are the caller's, as
    /// for <see cref="CheckIn"/>; the name is checked here.
    /// </summary>
    public static Result<Attendance> CheckInGuest(string guestName, Guid lockerId, DateTimeOffset checkedInAt)
    {
        var name = CleanGuestName(guestName);
        if (name.IsFailure)
        {
            return Result.Failure<Attendance>(name.Error);
        }

        return new Attendance
        {
            GuestName = name.Value,
            LockerId = lockerId,
            CheckedInAt = checkedInAt,
        };
    }

    /// <summary>A guest's visit on a reserve place, under the same conditions as a member's (BUSINESS_RULES.md §6).</summary>
    public static Result<Attendance> CheckInGuestOnReservePlace(string guestName, int reserveSlot, DateTimeOffset checkedInAt)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(reserveSlot, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(reserveSlot, ReservePlaceCount);

        var name = CleanGuestName(guestName);
        if (name.IsFailure)
        {
            return Result.Failure<Attendance>(name.Error);
        }

        return new Attendance
        {
            GuestName = name.Value,
            ReserveSlot = reserveSlot,
            CheckedInAt = checkedInAt,
        };
    }

    /// <summary>The same trim and limits as a member's name (BUSINESS_RULES.md §2, §7 <i>Guest visit</i>).</summary>
    private static Result<string> CleanGuestName(string? guestName)
    {
        var name = guestName?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return Result.Failure<string>(AttendanceErrors.GuestNameRequired);
        }

        if (name.Length > GuestNameMaxLength)
        {
            return Result.Failure<string>(AttendanceErrors.GuestNameTooLong);
        }

        return name;
    }

    /// <summary>
    /// Moves an open visit to another locker (BUSINESS_RULES.md §7 <i>Moving to another locker</i>).
    /// A visit on a reserve place gives it up. Whether the target is in service and free is the
    /// caller's check, the same split as check-in; nothing about the session, هوازی or the cafe
    /// changes.
    /// </summary>
    public Result MoveToLocker(Guid lockerId)
    {
        if (CheckedOutAt is not null)
        {
            return Result.Failure(AttendanceErrors.NotOpen);
        }

        if (LockerId == lockerId)
        {
            return Result.Failure(AttendanceErrors.SameLocker);
        }

        LockerId = lockerId;
        ReserveSlot = null;

        return Result.Success();
    }

    /// <summary>
    /// Only an open attendance can be checked out (BUSINESS_RULES.md §7). A guest cannot leave
    /// while a cafe order of the visit is unpaid: a member's debt stays on their account, but a
    /// guest has no account to leave it on (§7 <i>Guest visit</i>).
    /// </summary>
    /// <param name="hasUnpaidCafe">
    /// Whether any standing cafe order of this visit still owes money. Orders are another
    /// aggregate, so the caller answers; it is ignored on a member's visit.
    /// </param>
    public Result CheckOut(DateTimeOffset checkedOutAt, bool hasUnpaidCafe = false)
    {
        if (CheckedOutAt is not null)
        {
            return Result.Failure(AttendanceErrors.NotOpen);
        }

        if (IsGuest && hasUnpaidCafe)
        {
            return Result.Failure(AttendanceErrors.GuestHasUnpaidCafe);
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
    /// <param name="leavesUnpaidCafe">
    /// Whether a cafe order the desk did not tick for cancelling still owes money. On a guest's
    /// visit that refuses the cancellation, as it refuses a check-out (BUSINESS_RULES.md §7
    /// <i>Guest visit</i>); ignored on a member's visit.
    /// </param>
    public Result Cancel(DateTimeOffset now, int cancelWindowMinutes, bool leavesUnpaidCafe = false)
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

        if (IsGuest && leavesUnpaidCafe)
        {
            return Result.Failure(AttendanceErrors.GuestHasUnpaidCafe);
        }

        CancelledAt = now;
        CheckedOutAt = now;

        return Result.Success();
    }

    /// <summary>
    /// The nightly job's own close (BUSINESS_RULES.md §7 Auto-checkout): the session stays
    /// consumed, unlike <see cref="Cancel"/>. No <see cref="Result"/>, the same reasoning as
    /// <see cref="CheckIn"/> — the job only ever loads attendances it already queried as open,
    /// so there is nothing left here to check.
    /// </summary>
    public void AutoClose(DateTimeOffset closedAt)
    {
        CheckedOutAt = closedAt;
        AutoClosedAt = closedAt;
    }
}
