using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Payables;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Payables.UpdatePayable;

/// <summary>
/// Corrects a pending cheque or instalment (BUSINESS_RULES.md §9 <i>Cheques and instalments</i>).
/// The audit log keeps what it said before. A paid one goes back to pending first; a cancelled
/// one is final.
/// </summary>
/// <remarks>
/// Same two concurrency layers as <c>UpdateExpenseHandler</c>: the client's <c>Version</c> refuses
/// an edit made on stale data, and <c>xmin</c> refuses a save that races another edit or a mark.
/// </remarks>
public sealed class UpdatePayableHandler(IAppDbContext db)
{
    public async Task<Result<PayableResponse>> Handle(
        Guid id, UpdatePayableCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var payable = await db.Payables.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (payable is null)
        {
            return Result.Failure<PayableResponse>(PayableErrors.NotFound);
        }

        if (payable.Version != command.Version)
        {
            return Result.Failure<PayableResponse>(PayableErrors.ChangedConcurrently);
        }

        var updated = payable.Update(
            command.Kind,
            command.Amount,
            command.DueDate,
            command.Payee,
            command.Description,
            command.CategoryId,
            command.InstallmentNumber,
            command.InstallmentCount);
        if (updated.IsFailure)
        {
            return Result.Failure<PayableResponse>(updated.Error);
        }

        var category = await db.ExpenseCategories.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == command.CategoryId, cancellationToken);
        if (category is null)
        {
            return Result.Failure<PayableResponse>(PayableErrors.CategoryNotFound);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<PayableResponse>(PayableErrors.ChangedConcurrently);
        }

        return PayableResponse.From(payable, category.Name, expenseId: null);
    }
}
