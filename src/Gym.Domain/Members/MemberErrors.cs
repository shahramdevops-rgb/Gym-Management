using Gym.Domain.Common;

namespace Gym.Domain.Members;

public static class MemberErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Members.NotFound",
        "No member has that id.");

    /// <summary>Phone numbers are unique across all members, inactive ones included (BUSINESS_RULES.md §2).</summary>
    public static readonly Error PhoneAlreadyExists = Error.Conflict(
        "Members.PhoneAlreadyExists",
        "Another member already uses that phone number.");

    public static readonly Error PhoneInvalid = Error.Validation(
        "Members.PhoneInvalid",
        "The phone number is not valid.");

    /// <summary>A valid Iranian number that is not a mobile: it cannot receive SMS reminders.</summary>
    public static readonly Error PhoneNotMobile = Error.Validation(
        "Members.PhoneNotMobile",
        "The phone number must be a mobile number.");

    /// <summary>A valid number from another country. The gym registers Iranian numbers only.</summary>
    public static readonly Error PhoneNotIranian = Error.Validation(
        "Members.PhoneNotIranian",
        "The phone number must be an Iranian mobile number.");

    public static readonly Error PhoneRequired = Error.Validation(
        "Members.PhoneRequired",
        "Phone number is required.");

    public static readonly Error FullNameRequired = Error.Validation(
        "Members.FullNameRequired",
        "Full name is required.");

    public static readonly Error FullNameTooLong = Error.Validation(
        "Members.FullNameTooLong",
        "Full name is too long.");

    public static readonly Error NotesTooLong = Error.Validation(
        "Members.NotesTooLong",
        "Notes are too long.");

    /// <summary>Two people edited the same member at the same moment; the second save is refused.</summary>
    public static readonly Error ChangedConcurrently = Error.Conflict(
        "Members.ChangedConcurrently",
        "The member was changed by someone else at the same moment. Reload and try again.");
}
