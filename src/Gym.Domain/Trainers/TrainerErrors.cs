using Gym.Domain.Common;

namespace Gym.Domain.Trainers;

public static class TrainerErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Trainers.NotFound",
        "No trainer has that id.");

    /// <summary>Phones are unique among trainers, stopped ones included (BUSINESS_RULES.md §15).</summary>
    public static readonly Error PhoneAlreadyExists = Error.Conflict(
        "Trainers.PhoneAlreadyExists",
        "Another trainer already uses that phone number.");

    public static readonly Error FullNameRequired = Error.Validation(
        "Trainers.FullNameRequired",
        "Full name is required.");

    public static readonly Error FullNameTooLong = Error.Validation(
        "Trainers.FullNameTooLong",
        "Full name is too long.");

    public static readonly Error AlreadyStopped = Error.Conflict(
        "Trainers.AlreadyStopped",
        "The trainer is already stopped.");

    public static readonly Error AlreadyActive = Error.Conflict(
        "Trainers.AlreadyActive",
        "The trainer is already a trainer.");

    public static readonly Error ChangedConcurrently = Error.Conflict(
        "Trainers.ChangedConcurrently",
        "The trainer was changed by someone else. Reload and try again.");
}
