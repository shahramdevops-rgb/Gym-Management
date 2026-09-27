using Gym.Domain.Common;

namespace Gym.Domain.Lockers;

/// <summary>
/// One of the gym's lockers, which the front desk gives to a checked-in member (BUSINESS_RULES.md §6).
/// </summary>
/// <remarks>
/// <para>
/// The gym has exactly <see cref="Count"/> of them, numbered 1 to <see cref="Count"/>. They arrive
/// with the migration (<c>LockerSeed</c>) and nothing in the app creates or deletes one: a new
/// cabinet is a code change with a migration, agreed with the Owner first.
/// </para>
/// <para>
/// Occupancy is derived from Attendance (an open attendance referencing this locker), never
/// stored here, so it cannot drift out of sync with reality. Domain has no EF Core reference and
/// cannot query anything itself, so <see cref="MarkOutOfService"/> takes that fact as a
/// parameter instead of looking it up, the same way <c>Subscription.ConsumeSession</c> takes
/// today's date instead of asking a clock.
/// </para>
/// </remarks>
public sealed class Locker : Entity
{
    /// <summary>How many lockers the gym has (BUSINESS_RULES.md §6). The database checks the range too.</summary>
    public const int Count = 72;

    // For EF Core.
    private Locker()
    {
    }

    public int Number { get; private set; }

    public bool IsOutOfService { get; private set; }

    /// <summary>Postgres <c>xmin</c>, so a stale edit cannot overwrite a newer one.</summary>
    public uint Version { get; private set; }

    /// <summary>
    /// For the domain tests only. Internal, so no handler can create a locker: the real ones are
    /// seeded by the migration (BUSINESS_RULES.md §6).
    /// </summary>
    internal static Locker Create(int number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, Count);

        return new Locker { Number = number, IsOutOfService = false };
    }

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
