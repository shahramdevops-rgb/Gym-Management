using Gym.Domain.Common;

namespace Gym.Domain.Lockers;

/// <summary>
/// A numbered locker the gym assigns to a checked-in member (BUSINESS_RULES.md §6).
/// </summary>
/// <remarks>
/// Occupancy is derived from Attendance (an open attendance referencing this locker), never
/// stored here, so it cannot drift out of sync with reality. Domain has no EF Core reference and
/// cannot query anything itself, so <see cref="MarkOutOfService"/> takes that fact as a
/// parameter instead of looking it up, the same way <c>Subscription.ConsumeSession</c> takes
/// today's date instead of asking a clock.
/// </remarks>
public sealed class Locker : Entity
{
    public const int MinNumber = 1;

    // For EF Core.
    private Locker()
    {
    }

    public int Number { get; private set; }

    public bool IsOutOfService { get; private set; }

    /// <summary>Postgres <c>xmin</c>, so a stale edit cannot overwrite a newer one.</summary>
    public uint Version { get; private set; }

    public static Result<Locker> Create(int number)
    {
        var error = CheckNumber(number);
        if (error is not null)
        {
            return Result.Failure<Locker>(error);
        }

        return new Locker { Number = number, IsOutOfService = false };
    }

    public static Error? CheckNumber(int number) => number < MinNumber ? LockerErrors.NumberInvalid : null;

    /// <summary>Marking an already out-of-service locker out of service again succeeds and changes nothing.</summary>
    public Result MarkOutOfService(bool isOccupied)
    {
        if (isOccupied)
        {
            return Result.Failure(LockerErrors.Occupied);
        }

        IsOutOfService = true;
        return Result.Success();
    }

    /// <summary>Marking an already in-service locker in service again succeeds and changes nothing.</summary>
    public void MarkInService() => IsOutOfService = false;
}
