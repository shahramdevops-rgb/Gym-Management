namespace Gym.Application.History.ListSales;

/// <summary>
/// What kind of sale a row is (BUSINESS_RULES.md §12 <i>Sales in the history</i>). هوازی, فروشگاه and
/// آنالیز are all service charges in the database, but each is its own section on screen, so each is
/// its own source here; the names match <c>ServiceChargeKind</c>.
/// </summary>
public enum SaleSource
{
    /// <summary>A plan, single-session or membership (فروش پلن).</summary>
    Subscription,

    /// <summary>هوازی.</summary>
    Cardio,

    /// <summary>فروشگاه.</summary>
    Miscellaneous,

    /// <summary>آنالیز.</summary>
    Analysis,

    /// <summary>بوفه.</summary>
    CafeOrder,
}
