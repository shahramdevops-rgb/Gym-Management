using Gym.Application.Common;
using Gym.Domain.Payables;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Payables;

/// <summary>
/// Builds <see cref="PayableResponse"/>s: each needs its category's name and, once paid, its
/// standing expense, both in other tables. One query per table for a whole page, never per row.
/// </summary>
public static class PayableResponses
{
    public static async Task<PayableResponse> ForAsync(
        IAppDbContext db, Payable payable, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payable);

        var responses = await ForAsync(db, [payable], cancellationToken);

        return responses[0];
    }

    public static async Task<List<PayableResponse>> ForAsync(
        IAppDbContext db, IReadOnlyList<Payable> payables, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(payables);

        var categoryIds = payables.Select(payable => payable.CategoryId).Distinct().ToList();
        var categoryNames = await db.ExpenseCategories.AsNoTracking()
            .Where(category => categoryIds.Contains(category.Id))
            .ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);

        // At most one standing expense per payable: the partial unique index guarantees it.
        var paidIds = payables.Where(payable => payable.IsPaid).Select(payable => payable.Id).ToList();
        var expenseIds = new Dictionary<Guid, Guid>();
        if (paidIds.Count > 0)
        {
            expenseIds = await db.Expenses.AsNoTracking()
                .Where(expense => expense.PayableId != null && paidIds.Contains(expense.PayableId.Value) &&
                    expense.VoidedAt == null)
                .ToDictionaryAsync(expense => expense.PayableId!.Value, expense => expense.Id, cancellationToken);
        }

        return payables
            .Select(payable => PayableResponse.From(
                payable,
                categoryNames[payable.CategoryId],
                expenseIds.TryGetValue(payable.Id, out var expenseId) ? expenseId : null))
            .ToList();
    }
}
