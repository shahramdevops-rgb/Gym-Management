using System.Text.Json.Serialization;

using Gym.Domain.Payments;

namespace Gym.Application.Reports.GetFinancialReport;

/// <summary>
/// The figures behind the Owner's dashboard (BUSINESS_RULES.md §12 <i>Financial report</i>).
/// </summary>
/// <param name="Current">The range asked for.</param>
/// <param name="Previous">The range of the same length ending the day before it, to compare with.</param>
/// <param name="Days">Every day of <paramref name="Current"/>, oldest first; a day with nothing is zeros.</param>
public sealed record FinancialReportResponse(
    FinancialPeriodResponse Current,
    FinancialPeriodResponse Previous,
    IReadOnlyList<FinancialDayResponse> Days);

/// <summary>One range's money.</summary>
/// <param name="Revenue">Payments, refunds and their difference, by <c>PaidAt</c>.</param>
/// <param name="BySource">All seven sources, always in the same order, zeros included.</param>
/// <param name="ByMethod">All three methods in the desk's order: card, bank transfer, cash.</param>
/// <param name="ReceivedByMethod">
/// «دریافتی»: <paramref name="ByMethod"/> without فروشگاه, آنالیز and متفرقه, what should be in the gym's
/// drawer, card reader and account for the range (BUSINESS_RULES.md §12 <i>Financial report</i>).
/// </param>
/// <param name="ShopAndAnalysisByMethod">The money of فروشگاه and آنالیز alone, by method.</param>
/// <param name="OtherByMethod">
/// The money of متفرقه alone, by method: someone else's like فروشگاه and آنالیز, on a card of its own
/// (task 6.5.36).
/// </param>
/// <param name="ByStaff">Everyone who took or gave back money in the range, the largest net first.</param>
/// <param name="Sales">
/// What was sold in the range, cancelled and voided sales left out: plans, single visits, هوازی and
/// the cafe. فروشگاه, آنالیز and متفرقه are left out (BUSINESS_RULES.md §12 <i>Financial report</i>).
/// </param>
/// <param name="SalesPaid">
/// What has been paid so far on the sales counted in <paramref name="Sales"/>, refunds taken off,
/// whenever it was paid.
/// </param>
/// <param name="SalesOwed"><paramref name="Sales"/> minus <paramref name="SalesPaid"/>: still owed («نسیه»).</param>
/// <param name="Expenses">Expenses by <c>ExpenseDate</c>, voided ones left out.</param>
/// <param name="ExpensesByCategory">Each category with an expense in the range, the largest first.</param>
/// <param name="NetProfit">
/// Net revenue without فروشگاه, آنالیز and متفرقه, minus every expense (BUSINESS_RULES.md §12
/// <i>Financial report</i>).
/// </param>
public sealed record FinancialPeriodResponse(
    DateOnly From,
    DateOnly To,
    MoneyFlowResponse Revenue,
    IReadOnlyList<RevenueBySourceResponse> BySource,
    IReadOnlyList<RevenueByMethodResponse> ByMethod,
    IReadOnlyList<RevenueByMethodResponse> ReceivedByMethod,
    IReadOnlyList<RevenueByMethodResponse> ShopAndAnalysisByMethod,
    IReadOnlyList<RevenueByMethodResponse> OtherByMethod,
    IReadOnlyList<RevenueByStaffResponse> ByStaff,
    decimal Sales,
    decimal SalesPaid,
    decimal SalesOwed,
    decimal Expenses,
    IReadOnlyList<ExpensesByCategoryResponse> ExpensesByCategory,
    decimal NetProfit);

/// <param name="Refunded">The refunds, as a positive figure.</param>
/// <param name="Net">Received minus refunded.</param>
public sealed record MoneyFlowResponse(decimal Received, decimal Refunded, decimal Net);

/// <param name="Money">The money received for this source in the range, by <c>PaidAt</c>.</param>
/// <param name="Sold">
/// How many of this source were sold in the range, by the day each sale belongs to, cancelled and
/// voided sales left out: the rule <see cref="FinancialPeriodResponse.Sales"/> follows, counted
/// rather than summed. Plans and single visits are counted one by one; a cafe order is one sale
/// however many items it holds. It does not follow <paramref name="Money"/>: a plan sold before the
/// range and paid in it adds money here, not a sale.
/// </param>
/// <param name="SoldAmount">
/// The price of those same sales, paid or not: the figure of «خرید پلن» and «تک‌جلسه‌ای» on the
/// dashboard, by the day each was sold (decided with the developer, 1405/07/14).
/// </param>
/// <param name="SoldPaid">
/// What has been paid so far on those same sales, refunds taken off, whenever it was paid
/// («پرداخت‌شده» on «خرید پلن» and «تک‌جلسه‌ای», 1405/07/14).
/// </param>
/// <param name="SoldOwed"><paramref name="SoldAmount"/> minus <paramref name="SoldPaid"/>: still owed («نسیه»).</param>
public sealed record RevenueBySourceResponse(
    [property: JsonConverter(typeof(JsonStringEnumConverter<RevenueSource>))] RevenueSource Source,
    MoneyFlowResponse Money,
    int Sold,
    decimal SoldAmount,
    decimal SoldPaid,
    decimal SoldOwed);

public sealed record RevenueByMethodResponse(
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentMethod>))] PaymentMethod Method,
    MoneyFlowResponse Money);

/// <param name="UserId">Who took the payments and gave the refunds (<c>ReceivedByUserId</c>).</param>
public sealed record RevenueByStaffResponse(Guid UserId, string FullName, MoneyFlowResponse Money);

/// <param name="Name">The category's name today: a renamed category is shown by its new name.</param>
public sealed record ExpensesByCategoryResponse(Guid CategoryId, string Name, decimal Amount);

/// <param name="Revenue">Net revenue of the day.</param>
public sealed record FinancialDayResponse(DateOnly Date, decimal Revenue, decimal Expenses);
