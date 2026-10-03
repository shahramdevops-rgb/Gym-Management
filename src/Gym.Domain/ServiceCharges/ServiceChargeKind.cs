namespace Gym.Domain.ServiceCharges;

/// <summary>
/// What the member used or bought during the visit (BUSINESS_RULES.md §7 <i>Gym services</i>).
/// Sauna or massage would be new members of this enum, not new tables: they are the same thing —
/// money owed for something used during a visit — priced the same way, paid the same way.
/// </summary>
public enum ServiceChargeKind
{
    /// <summary>هوازی, the treadmill. At most one standing charge per visit.</summary>
    Cardio,

    /// <summary>
    /// فروشگاه: something sold at the desk that is neither on the cafe's price list nor a service
    /// the system knows (§7 <i>Sale at the desk</i>). The desk types its name, quantity and unit
    /// price; a visit may have any number of them.
    /// </summary>
    Miscellaneous,

    /// <summary>
    /// آنالیز: a single typed price, any number per visit, under a source of its own (§7 <i>Sale at
    /// the desk</i>, task 6.5.29). Otherwise it follows the rules of <see cref="Miscellaneous"/>.
    /// </summary>
    Analysis,
}
