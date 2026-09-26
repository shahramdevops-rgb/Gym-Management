using Gym.Domain.Common;

namespace Gym.Application.Accounts;

/// <summary>Failures of the server console's account commands. Printed, never shown in the web app.</summary>
public static class AccountErrors
{
    public static readonly Error UserNotFound = Error.NotFound(
        "Accounts.UserNotFound",
        "No account has that user name.");

    public static readonly Error UserNameTaken = Error.Conflict(
        "Accounts.UserNameTaken",
        "Another account already uses that user name.");

    public static readonly Error UserNameInvalid = Error.Validation(
        "Accounts.UserNameInvalid",
        $"User names are {Common.Security.UserNamePolicy.MinimumLength} to {Common.Security.UserNamePolicy.MaximumLength} characters: Latin letters, digits and - . _ @ +.");

    public static readonly Error UserNameGuessable = Error.Validation(
        "Accounts.UserNameGuessable",
        "That user name is too easy to guess (admin, owner, manager, ...).");

    public static readonly Error ChangedConcurrently = Error.Conflict(
        "Accounts.ChangedConcurrently",
        "The account was changed by another request at the same moment. Try again.");

    /// <summary>Identity refused for a reason the policy does not name. Should not happen.</summary>
    public static Error Rejected(string description) => Error.Validation("Accounts.Rejected", description);
}
