namespace Gym.Application.ServiceCharges;

/// <summary>
/// What a miscellaneous sale sold (BUSINESS_RULES.md §7 <i>Miscellaneous sale</i>), for the lists
/// that name an owed or paid item: the debt breakdown and the payment histories.
/// </summary>
public sealed record MiscellaneousSaleSummary(string Description, int Quantity, decimal UnitPrice)
{
    /// <summary>
    /// <c>null</c> for هوازی, whose three columns are null; the database's check constraint keeps
    /// them all set or all null, so one of them is enough to tell.
    /// </summary>
    public static MiscellaneousSaleSummary? From(string? description, int? quantity, decimal? unitPrice) =>
        description is null || quantity is null || unitPrice is null
            ? null
            : new MiscellaneousSaleSummary(description, quantity.Value, unitPrice.Value);
}
