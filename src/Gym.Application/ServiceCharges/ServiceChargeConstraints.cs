namespace Gym.Application.ServiceCharges;

/// <summary>
/// Database constraint names the service charge handlers react to, shared with the migration
/// that creates them.
/// </summary>
public static class ServiceChargeConstraints
{
    /// <summary>
    /// One non-voided charge per visit per kind (BUSINESS_RULES.md §7 <i>Gym services</i>). The
    /// handler checks it first; this is the backstop for two desks recording at the same moment.
    /// </summary>
    public const string OneLivePerVisitAndKind = "ix_service_charges_one_live_per_visit_and_kind";
}
