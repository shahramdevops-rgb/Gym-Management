namespace Gym.Domain.ServiceCharges;

/// <summary>
/// What the member used during the visit (BUSINESS_RULES.md §7 <i>Gym services</i>). Sauna or
/// massage would be new members of this enum, not new tables: they are the same thing — money
/// owed for something used during a visit — priced the same way, paid the same way.
/// </summary>
public enum ServiceChargeKind
{
    /// <summary>هوازی, the treadmill. The only kind today.</summary>
    Cardio,
}
