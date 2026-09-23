using Gym.Application.Common;
using Gym.Application.ServiceCharges;
using Gym.Domain.Attendances;
using Gym.Domain.Common;
using Gym.Domain.Subscriptions;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Attendances.CancelCheckIn;

/// <summary>
/// Cancels an open check-in within the allowed window (BUSINESS_RULES.md §7): restores the
/// session, frees the locker (occupancy is derived from <c>CheckedOutAt</c>, which
/// <see cref="Attendance.Cancel"/> also sets), and voids whatever the visit was charged for.
/// Front desk work, so both roles.
/// </summary>
public sealed class CancelCheckInHandler(IAppDbContext db, IAttendancePolicy policy, TimeProvider time, ICurrentUser currentUser)
{
    /// <summary>
    /// Stored on the charges this cancellation voids. English like every other value the
    /// application writes itself; the reasons staff type are their own words.
    /// </summary>
    private const string VoidReason = "The check-in was cancelled.";

    public async Task<Result<AttendanceResponse>> Handle(Guid id, CancellationToken cancellationToken)
    {
        var attendance = await db.Attendances.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (attendance is null)
        {
            return Result.Failure<AttendanceResponse>(AttendanceErrors.NotFound);
        }

        var now = time.GetUtcNow();

        // The endpoint's policy requires an authenticated user, so this is a wiring bug if hit.
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Cancel check-in was called without an authenticated user.");

        // One transaction, taking the member's lock, because the charges below are voided from
        // what has been paid against them and a payment for this member may be committing right
        // now — the same lock registering that payment takes.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockMemberAsync(attendance.MemberId, cancellationToken);

        var cancelled = attendance.Cancel(now, policy.CancelWindowMinutes);
        if (cancelled.IsFailure)
        {
            return Result.Failure<AttendanceResponse>(cancelled.Error);
        }

        var subscription = await db.Subscriptions.SingleAsync(s => s.Id == attendance.SubscriptionId, cancellationToken);
        subscription.RestoreSession();

        // BUSINESS_RULES.md §7: money for a visit that never happened is not owed. Anything
        // already collected goes back the way it came, because a voided charge must not leave the
        // gym holding money against a member's name (§5: no wallet, no credit balance).
        var charges = await db.ServiceCharges
            .Where(charge => charge.AttendanceId == id && charge.VoidedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var charge in charges)
        {
            var voided = charge.Void(VoidReason, now, userId);
            if (voided.IsFailure)
            {
                // Only an already-voided charge can fail here, and the query above excluded those.
                throw new InvalidOperationException(
                    $"Voiding a service charge while cancelling a check-in failed: {voided.Error.Code}.");
            }

            await ServiceChargeRefunder.RefundNetPaidAsync(db, charge, VoidReason, userId, now, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var lockerNumber = attendance.LockerId is null
            ? null
            : await db.Lockers.Where(l => l.Id == attendance.LockerId).Select(l => (int?)l.Number).SingleOrDefaultAsync(cancellationToken);

        // The charges are voided, so the visit is left with none — the same list the front desk
        // sees everywhere else.
        return AttendanceResponse.From(attendance, lockerNumber);
    }
}
