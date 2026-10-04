namespace Gym.Application.History.PaymentTotals;

/// <summary>
/// What the «پرداخت‌ها» section comes to, over every payment and refund its filters let through
/// (BUSINESS_RULES.md §12 <i>Totals in the history</i>).
/// </summary>
/// <param name="Received">The payments («دریافتی»).</param>
/// <param name="Refunded">The refunds («بازگشت»), as a positive figure.</param>
/// <param name="Net">Payments minus refunds («خالص»).</param>
public sealed record PaymentTotalsResponse(decimal Received, decimal Refunded, decimal Net);
