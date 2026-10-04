namespace Gym.Application.History.SalesTotals;

/// <summary>
/// What a sales section comes to, over every sale its filters let through (BUSINESS_RULES.md §12
/// <i>Totals in the history</i>). Cancelled and voided sales count toward none of the three.
/// </summary>
/// <param name="Amount">What the sales were sold for («مبلغ»).</param>
/// <param name="NetPaid">Payments minus refunds on them («دریافتی»).</param>
/// <param name="Remaining">
/// What is still owed («مانده»): each sale's amount minus its net paid, never below zero, added up.
/// </param>
public sealed record SalesTotalsResponse(decimal Amount, decimal NetPaid, decimal Remaining);
