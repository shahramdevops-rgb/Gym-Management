using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Lockers;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Lockers.SetLockerOutOfService;

/// <summary>
/// Takes a locker out of service or brings it back in. BUSINESS_RULES.md §6: a locker cannot be
/// taken out of service while occupied. Staff may do both — the person who finds a locker broken
/// is the one at the desk, and the same person sees it repaired.
/// </summary>
public sealed class SetLockerOutOfServiceHandler(IAppDbContext db)
{
    public async Task<Result<LockerResponse>> MarkOutOfService(Guid id, CancellationToken cancellationToken)
    {
        var locker = await db.Lockers.SingleOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (locker is null)
        {
            return Result.Failure<LockerResponse>(LockerErrors.NotFound);
        }

        var holder = await FindHolderAsync(id, cancellationToken);

        var result = locker.MarkOutOfService(isOccupied: holder is not null);
        if (result.IsFailure)
        {
            return Result.Failure<LockerResponse>(result.Error);
        }

        return await SaveAsync(locker, holder, cancellationToken);
    }

    public async Task<Result<LockerResponse>> MarkInService(Guid id, CancellationToken cancellationToken)
    {
        var locker = await db.Lockers.SingleOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (locker is null)
        {
            return Result.Failure<LockerResponse>(LockerErrors.NotFound);
        }

        locker.MarkInService();

        var holder = await FindHolderAsync(id, cancellationToken);

        return await SaveAsync(locker, holder, cancellationToken);
    }

    /// <summary>
    /// Who holds the locker through its open attendance, a member or a guest, or <c>null</c>. One
    /// query rather than an existence check followed by a lookup: the caller needs both answers,
    /// and a locker has at most one open attendance (partial unique index, BUSINESS_RULES.md §7).
    /// </summary>
    /// <remarks>
    /// The member is looked up beside the visit rather than joined to it: an inner join would drop
    /// a guest's visit, which has no member, and read a guest-held locker as free.
    /// </remarks>
    private Task<LockerHolder?> FindHolderAsync(Guid lockerId, CancellationToken cancellationToken) =>
        db.Attendances
            .Where(a => a.LockerId == lockerId && a.CheckedOutAt == null)
            .Select(a => new LockerHolder(
                a.MemberId,
                db.Members.Where(m => m.Id == a.MemberId).Select(m => m.FullName).FirstOrDefault(),
                a.GuestName,
                a.IsCardioOnly))
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<Result<LockerResponse>> SaveAsync(Locker locker, LockerHolder? holder, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<LockerResponse>(LockerErrors.ChangedConcurrently);
        }

        return LockerResponse.From(locker, holder);
    }
}
