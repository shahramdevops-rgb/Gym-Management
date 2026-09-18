using Gym.Domain.Common;

namespace Gym.Application.Staff;

public static class StaffErrors
{
    /// <summary>
    /// Also the answer for an Owner account's id: these endpoints manage Staff only, and the
    /// Owner must not be able to deactivate or reset themselves by mistake.
    /// </summary>
    public static readonly Error NotFound = Error.NotFound(
        "Staff.NotFound",
        "No staff account has that id.");

    public static readonly Error ChangedConcurrently = Error.Conflict(
        "Staff.ChangedConcurrently",
        "The account was changed by another request at the same moment. Try again.");

    public static readonly Error UserNameTaken = Error.Conflict(
        "Staff.UserNameTaken",
        "Another account already uses that user name.");

    /// <summary>
    /// Identity refused the account. The validator checks the same rules first, so this only
    /// appears if the two ever disagree.
    /// </summary>
    public static Error Rejected(string description) => Error.Validation("Staff.Rejected", description);
}
