using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Lockers;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Lockers.SetLockerOutOfService;

/// <summary>
/// Takes a locker out of service or brings it back in. BUSINESS_RULES.md §6: a locker cannot be
/// taken out of service while occupied.
/// </summary>
/// <remarks>
/// Occupancy always passes as <see langword="false"/> here until task 5.2 adds the Attendance
/// entity (see <see cref="LockerResponse"/>): there is no way yet for a check-in to exist, so
/// nothing can be occupied. The domain rule itself is fully exercised by <c>LockerTests</c>,
/// which calls <see cref="Locker.MarkOutOfService"/> directly with both values.
/// </remarks>
public sealed class SetLockerOutOfServiceHandler(IAppDbContext db)
{
    public async Task<Result<LockerResponse>> MarkOutOfService(Guid id, CancellationToken cancellationToken)
    {
        var locker = await db.Lockers.SingleOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (locker is null)
        {
            return Result.Failure<LockerResponse>(LockerErrors.NotFound);
        }

        var result = locker.MarkOutOfService(isOccupied: false);
        if (result.IsFailure)
        {
            return Result.Failure<LockerResponse>(result.Error);
        }

        return await SaveAsync(locker, cancellationToken);
    }

    public async Task<Result<LockerResponse>> MarkInService(Guid id, CancellationToken cancellationToken)
    {
        var locker = await db.Lockers.SingleOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (locker is null)
        {
            return Result.Failure<LockerResponse>(LockerErrors.NotFound);
        }

        locker.MarkInService();

        return await SaveAsync(locker, cancellationToken);
    }

    private async Task<Result<LockerResponse>> SaveAsync(Locker locker, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<LockerResponse>(LockerErrors.ChangedConcurrently);
        }

        return LockerResponse.From(locker);
    }
}
