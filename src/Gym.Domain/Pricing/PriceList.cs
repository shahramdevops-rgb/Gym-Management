using Gym.Domain.Common;

namespace Gym.Domain.Pricing;

/// <summary>
/// The gym's two prices (BUSINESS_RULES.md §3 <i>Prices</i>): one session of a plan, and one
/// single-session visit. The Owner sets them on the settings screen; everyone sells at them.
/// </summary>
/// <remarks>
/// <para>
/// <b>One row.</b> There is exactly one price list, seeded by the migration with <see cref="TheId"/>
/// and both prices empty. Nothing ever creates a second one, and a check constraint on the id makes
/// the database refuse it too, so "which price list?" never has a second answer.
/// </para>
/// <para>
/// <b>Empty until the Owner sets them.</b> The prices are nullable on purpose: they are the gym's own
/// numbers, and a seeded 0 would be a price nobody chose that the desk could sell at. A subscription
/// asks for the price it needs and hears <see cref="PricingErrors.SessionPriceNotSet"/> or
/// <see cref="PricingErrors.SingleVisitPriceNotSet"/> instead.
/// </para>
/// <para>
/// <b>Changing a price never reaches a past sale.</b> A subscription copies its price when it is
/// sold (§4), the way a printed receipt keeps the price of the day.
/// </para>
/// </remarks>
public sealed class PriceList : Entity
{
    /// <summary>The column is <c>numeric(18,2)</c>: 16 digits before the point, 2 after.</summary>
    public const int PriceDecimals = 2;

    public const decimal MaxPrice = 9_999_999_999_999_999.99m;

    /// <summary>The id of the one row, seeded by the migration.</summary>
    public static readonly Guid TheId = new("3f1c9a52-7d4e-4b8a-9c21-5e6f7a8b9c0d");

    // For EF Core. The row comes from the migration, so no code creates one.
    private PriceList()
    {
    }

    /// <summary>One session of a plan. A plan costs its sessions times this.</summary>
    public decimal? SessionPrice { get; private set; }

    /// <summary>One single-session (تک‌جلسه‌ای) visit.</summary>
    public decimal? SingleVisitPrice { get; private set; }

    /// <summary>Postgres <c>xmin</c>, so a stale edit cannot overwrite a newer one.</summary>
    public uint Version { get; private set; }

    /// <summary>
    /// Sets both prices together. Neither can be cleared once set: a screen that could empty a price
    /// would stop the desk selling until someone noticed.
    /// </summary>
    public Result Update(decimal sessionPrice, decimal singleVisitPrice)
    {
        var error = CheckPrice(sessionPrice) ?? CheckPrice(singleVisitPrice);
        if (error is not null)
        {
            return Result.Failure(error);
        }

        SessionPrice = sessionPrice;
        SingleVisitPrice = singleVisitPrice;

        return Result.Success();
    }

    /// <summary>
    /// The money rule every price follows. Also used by the Application validator, so the form hears
    /// the same answer as the entity. Too many decimals is refused rather than rounded: rounding money
    /// silently is how "100,000.005" becomes a price nobody typed.
    /// </summary>
    public static Error? CheckPrice(decimal price)
    {
        if (price < 0)
        {
            return PricingErrors.PriceNegative;
        }

        if (price > MaxPrice)
        {
            return PricingErrors.PriceTooLarge;
        }

        return decimal.Round(price, PriceDecimals) != price ? PricingErrors.PriceTooManyDecimals : null;
    }
}
