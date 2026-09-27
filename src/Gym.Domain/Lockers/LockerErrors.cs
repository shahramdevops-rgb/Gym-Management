using Gym.Domain.Common;

namespace Gym.Domain.Lockers;

public static class LockerErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Lockers.NotFound",
        "No locker has that id.");

    /// <summary>BUSINESS_RULES.md §6: a locker cannot be marked out of service while occupied.</summary>
    public static readonly Error Occupied = Error.BusinessRule(
        "Lockers.Occupied",
        "The locker is occupied and cannot be marked out of service.");

    /// <summary>BUSINESS_RULES.md §7: check-in and moving a visit only take a locker that is in service.</summary>
    public static readonly Error OutOfService = Error.BusinessRule(
        "Lockers.OutOfService",
        "The locker is out of service.");

    /// <summary>Two people changed the same locker at the same moment; the second save is refused.</summary>
    public static readonly Error ChangedConcurrently = Error.Conflict(
        "Lockers.ChangedConcurrently",
        "The locker was changed by someone else at the same moment. Reload and try again.");
}
