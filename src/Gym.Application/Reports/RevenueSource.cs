namespace Gym.Application.Reports;

/// <summary>
/// What money was received for (BUSINESS_RULES.md §12 <i>Financial report</i>). A payment points at
/// a subscription, a service charge or a cafe order; the report splits the first by whether it is a
/// single visit and the second by its kind, so the Owner reads six figures, not three.
/// </summary>
public enum RevenueSource
{
    /// <summary>A membership plan (فروش پلن).</summary>
    Membership,

    /// <summary>A single visit (تک‌جلسه‌ای), apart from membership.</summary>
    SingleSession,

    /// <summary>هوازی.</summary>
    Cardio,

    /// <summary>فروشگاه.</summary>
    Miscellaneous,

    /// <summary>آنالیز.</summary>
    Analysis,

    /// <summary>بوفه.</summary>
    Cafe,
}
