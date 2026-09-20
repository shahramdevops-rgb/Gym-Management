using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Lockers;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Lockers.GetLocker;

/// <summary>One locker.</summary>
public sealed class GetLockerHandler(IAppDbContext db)
{
    public async Task<Result<LockerResponse>> Handle(Guid id, CancellationToken cancellationToken)
    {
        var openAttendances = db.Attendances.Where(a => a.CheckedOutAt == null);

        var locker = await db.Lockers
            .AsNoTracking()
            .Where(l => l.Id == id)
            .Select(LockerResponse.Projection(openAttendances))
            .SingleOrDefaultAsync(cancellationToken);

        return locker is null ? Result.Failure<LockerResponse>(LockerErrors.NotFound) : locker;
    }
}
