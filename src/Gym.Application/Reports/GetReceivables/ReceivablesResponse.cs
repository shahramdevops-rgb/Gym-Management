namespace Gym.Application.Reports.GetReceivables;

/// <summary>
/// What everyone owes the gym now, and how old it is (BUSINESS_RULES.md §12 <i>Receivables</i>).
/// The three ages add up to <paramref name="Total"/>.
/// </summary>
/// <param name="Total">Every live sale's amount minus net paid, never below zero per sale.</param>
/// <param name="UpTo7Days">Owed on sales recorded today or in the 7 days before.</param>
/// <param name="From8To30Days">Owed on sales recorded 8 to 30 days ago.</param>
/// <param name="Over30Days">Owed on sales recorded more than 30 days ago.</param>
public sealed record ReceivablesResponse(
    decimal Total,
    decimal UpTo7Days,
    decimal From8To30Days,
    decimal Over30Days);
