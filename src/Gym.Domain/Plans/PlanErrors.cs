using Gym.Domain.Common;

namespace Gym.Domain.Plans;

public static class PlanErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Plans.NotFound",
        "No plan has that id.");

    /// <summary>
    /// Names are unique among all plans, inactive ones included, compared in normalized form
    /// (decided with the developer in task 3.1).
    /// </summary>
    public static readonly Error NameAlreadyExists = Error.Conflict(
        "Plans.NameAlreadyExists",
        "Another plan already uses that name.");

    public static readonly Error NameRequired = Error.Validation(
        "Plans.NameRequired",
        "Plan name is required.");

    public static readonly Error NameTooLong = Error.Validation(
        "Plans.NameTooLong",
        "Plan name is too long.");

    public static readonly Error DurationInvalid = Error.Validation(
        "Plans.DurationInvalid",
        $"Duration must be between 1 and {Plan.MaxDurationDays} days.");

    public static readonly Error SessionCountInvalid = Error.Validation(
        "Plans.SessionCountInvalid",
        $"Session count must be empty (unlimited) or between 1 and {Plan.MaxSessionCount}.");

    public static readonly Error PriceNegative = Error.Validation(
        "Plans.PriceNegative",
        "Price cannot be negative.");

    public static readonly Error PriceTooLarge = Error.Validation(
        "Plans.PriceTooLarge",
        "Price is too large.");

    public static readonly Error PriceTooManyDecimals = Error.Validation(
        "Plans.PriceTooManyDecimals",
        $"Price can have at most {Plan.PriceDecimals} decimal places.");

    /// <summary>BUSINESS_RULES.md §3: inactive plans cannot be sold.</summary>
    public static readonly Error Inactive = Error.BusinessRule(
        "Plans.Inactive",
        "The plan is inactive and cannot be sold.");

    /// <summary>Two people edited the same plan at the same moment; the second save is refused.</summary>
    public static readonly Error ChangedConcurrently = Error.Conflict(
        "Plans.ChangedConcurrently",
        "The plan was changed by someone else at the same moment. Reload and try again.");
}
