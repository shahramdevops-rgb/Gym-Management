using Gym.Domain.Common;

namespace Gym.Domain.Lockers;

public static class LockerErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Lockers.NotFound",
        "No locker has that id.");

    public static readonly Error NumberInvalid = Error.Validation(
        "Lockers.NumberInvalid",
        "Locker number must be a positive whole number.");

    public static readonly Error NumberAlreadyExists = Error.Conflict(
        "Lockers.NumberAlreadyExists",
        "Another locker already uses that number.");

    /// <summary>BUSINESS_RULES.md §6: a locker cannot be marked out of service while occupied.</summary>
    public static readonly Error Occupied = Error.BusinessRule(
        "Lockers.Occupied",
        "The locker is occupied and cannot be marked out of service.");

    /// <summary>Two people changed the same locker at the same moment; the second save is refused.</summary>
    public static readonly Error ChangedConcurrently = Error.Conflict(
        "Lockers.ChangedConcurrently",
        "The locker was changed by someone else at the same moment. Reload and try again.");
}
