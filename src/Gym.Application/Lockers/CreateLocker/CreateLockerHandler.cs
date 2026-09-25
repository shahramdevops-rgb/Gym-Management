using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Lockers;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Lockers.CreateLocker;

/// <summary>Adds a locker to the gym's setup (BUSINESS_RULES.md §6). Owner only.</summary>
public sealed class CreateLockerHandler(IAppDbContext db)
{
    public async Task<Result<LockerResponse>> Handle(CreateLockerCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var created = Locker.Create(command.Number);
        if (created.IsFailure)
        {
            return Result.Failure<LockerResponse>(created.Error);
        }

        var locker = created.Value;
        if (await db.Lockers.AnyAsync(l => l.Number == locker.Number, cancellationToken))
        {
            return Result.Failure<LockerResponse>(LockerErrors.NumberAlreadyExists);
        }

        // Two requests can pass the check above at once; the unique index decides.
        db.Lockers.Add(locker);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (exception.ConstraintName == LockerConstraints.UniqueNumber)
        {
            return Result.Failure<LockerResponse>(LockerErrors.NumberAlreadyExists);
        }

        // A locker that was just created cannot already have an open attendance against it.
        return LockerResponse.From(locker, holder: null);
    }
}
