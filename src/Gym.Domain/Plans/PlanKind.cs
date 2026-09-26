namespace Gym.Domain.Plans;

/// <summary>
/// What kind of thing the plan sells (BUSINESS_RULES.md §3 <i>The single-session plan</i>).
/// </summary>
/// <remarks>
/// The distinction is not cosmetic: a <see cref="SingleSession"/> sale is deliberately invisible to
/// every rule that orders a member's calendar (§4 <i>Single-session subscriptions</i>), because it is
/// one day for one visit and must never move, delay or shorten what the member already bought.
/// </remarks>
public enum PlanKind
{
    /// <summary>The ordinary case: a term of days with a number of sessions, or unlimited.</summary>
    Membership,

    /// <summary>
    /// تک‌جلسه‌ای: one visit, today only. Always one day and one session, and there is exactly one
    /// such plan in the system — the gym charges a walk-in visitor a single rate.
    /// </summary>
    SingleSession,
}
