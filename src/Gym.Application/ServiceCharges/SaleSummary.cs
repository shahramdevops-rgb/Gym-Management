namespace Gym.Application.ServiceCharges;

/// <summary>
/// What a sale sold, فروشگاه or آنالیز (BUSINESS_RULES.md §7 <i>Sale at the desk</i>), for the
/// lists that name an owed or paid item: the debt breakdown and the payment histories.
/// </summary>
public sealed record SaleSummary(string Description, int Quantity, decimal UnitPrice)
{
    /// <summary>
    /// <c>null</c> for هوازی, whose three columns are null; the database's check constraint keeps
    /// them all set or all null, so one of them is enough to tell.
    /// </summary>
    public static SaleSummary? From(string? description, int? quantity, decimal? unitPrice) =>
        description is null || quantity is null || unitPrice is null
            ? null
            : new SaleSummary(description, quantity.Value, unitPrice.Value);
}
