using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Lockers;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Lockers.SetLockerOutOfService;

/// <summary>
/// Takes a locker out of service or brings it back in. BUSINESS_RULES.md §6: a locker cannot be
/// taken out of service while occupied.
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

        var isOccupied = await db.Attendances.AnyAsync(a => a.LockerId == id && a.CheckedOutAt == null, cancellationToken);

        var result = locker.MarkOutOfService(isOccupied);
        if (result.IsFailure)
        {
            return Result.Failure<LockerResponse>(result.Error);
        }

        return await SaveAsync(locker, isOccupied, cancellationToken);
    }

    public async Task<Result<LockerResponse>> MarkInService(Guid id, CancellationToken cancellationToken)
    {
        var locker = await db.Lockers.SingleOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (locker is null)
        {
            return Result.Failure<LockerResponse>(LockerErrors.NotFound);
        }

        locker.MarkInService();

        var isOccupied = await db.Attendances.AnyAsync(a => a.LockerId == id && a.CheckedOutAt == null, cancellationToken);

        return await SaveAsync(locker, isOccupied, cancellationToken);
    }

    private async Task<Result<LockerResponse>> SaveAsync(Locker locker, bool isOccupied, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<LockerResponse>(LockerErrors.ChangedConcurrently);
        }

        return LockerResponse.From(locker, isOccupied);
    }
}
