using Gym.Domain.Common;

namespace Gym.Domain.Pricing;

public static class PricingErrors
{
    public static readonly Error PriceNegative = Error.Validation(
        "Pricing.PriceNegative",
        "Price cannot be negative.");

    public static readonly Error PriceTooLarge = Error.Validation(
        "Pricing.PriceTooLarge",
        "Price is too large.");

    public static readonly Error PriceTooManyDecimals = Error.Validation(
        "Pricing.PriceTooManyDecimals",
        $"Price can have at most {PriceList.PriceDecimals} decimal places.");

    /// <summary>BUSINESS_RULES.md §3: a plan cannot be sold before the Owner sets the session price.</summary>
    public static readonly Error SessionPriceNotSet = Error.BusinessRule(
        "Pricing.SessionPriceNotSet",
        "The session price has not been set yet. The Owner sets it in settings.");

    /// <summary>BUSINESS_RULES.md §3: a single visit cannot be sold before the Owner sets its price.</summary>
    public static readonly Error SingleVisitPriceNotSet = Error.BusinessRule(
        "Pricing.SingleVisitPriceNotSet",
        "The single-visit price has not been set yet. The Owner sets it in settings.");

    /// <summary>Two people saved the prices at the same moment; the second save is refused.</summary>
    public static readonly Error ChangedConcurrently = Error.Conflict(
        "Pricing.ChangedConcurrently",
        "The prices were changed by someone else at the same moment. Reload and try again.");
}
