using Gym.Application.Common;
using Gym.Domain.Attendances;
using Gym.Domain.Common;
using Gym.Domain.Lockers;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Attendances;

/// <summary>
/// The checks a locker the desk chose must pass, shared by check-in and moving a visit so the
/// two cannot disagree (BUSINESS_RULES.md §7: "under the same checks and errors as check-in").
/// </summary>
/// <remarks>
/// These are reads, and another desk can take the locker between the read and the save. The
/// partial unique index on the locker settles that race; the caller maps its violation to
/// <see cref="AttendanceErrors.LockerTaken"/>, the same error this returns.
/// </remarks>
public static class LockerChoice
{
    /// <summary>The locker's number when it exists, is in service and has no open visit.</summary>
    public static async Task<Result<int>> CheckAsync(IAppDbContext db, Guid lockerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        var locker = await db.Lockers
            .AsNoTracking()
            .Where(l => l.Id == lockerId)
            .Select(l => new
            {
                l.Number,
                l.IsOutOfService,
                IsTaken = db.Attendances.Any(a => a.LockerId == l.Id && a.CheckedOutAt == null),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (locker is null)
        {
            return Result.Failure<int>(LockerErrors.NotFound);
        }

        if (locker.IsOutOfService)
        {
            return Result.Failure<int>(LockerErrors.OutOfService);
        }

        if (locker.IsTaken)
        {
            return Result.Failure<int>(AttendanceErrors.LockerTaken);
        }

        return locker.Number;
    }

    /// <summary>Whether any locker is both in service and free, which rules out a reserve place (BUSINESS_RULES.md §6).</summary>
    public static Task<bool> AnyFreeAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        return db.Lockers.AnyAsync(
            l => !l.IsOutOfService && !db.Attendances.Any(a => a.LockerId == l.Id && a.CheckedOutAt == null),
            cancellationToken);
    }

    /// <summary>
    /// The lowest reserve place not held by an open visit (BUSINESS_RULES.md §6), for a member's
    /// check-in and a guest's alike. Which one does not matter — its number is never shown — so
    /// lowest is simply the easiest to reason about.
    /// </summary>
    /// <remarks>
    /// Two desks asking at once can both read the same place as free. The partial unique index on
    /// the place refuses the second, which then hears "try again", and the retry reads afresh.
    /// </remarks>
    public static async Task<Result<int>> FreeReserveSlotAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        if (await AnyFreeAsync(db, cancellationToken))
        {
            return Result.Failure<int>(AttendanceErrors.LockersStillFree);
        }

        var held = await db.Attendances
            .Where(a => a.CheckedOutAt == null && a.ReserveSlot != null)
            .Select(a => a.ReserveSlot!.Value)
            .ToListAsync(cancellationToken);

        var free = Enumerable.Range(1, Attendance.ReservePlaceCount).Except(held).ToList();

        return free.Count == 0 ? Result.Failure<int>(AttendanceErrors.ReserveFull) : free[0];
    }
}
