using Gym.Application.Common;
using Gym.Domain.Cheques;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Cheques.UpdateCheque;

/// <summary>
/// Corrects a pending cheque (BUSINESS_RULES.md §9 <i>Cheques</i>). The audit log keeps what it
/// said before. A passed or cancelled cheque is final.
/// </summary>
/// <remarks>
/// Same two concurrency layers as <c>UpdateExpenseHandler</c>: the client's <c>Version</c> refuses
/// an edit made on stale data, and <c>xmin</c> refuses a save that races another edit or a mark.
/// </remarks>
public sealed class UpdateChequeHandler(IAppDbContext db)
{
    public async Task<Result<ChequeResponse>> Handle(
        Guid id, UpdateChequeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var cheque = await db.Cheques.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (cheque is null)
        {
            return Result.Failure<ChequeResponse>(ChequeErrors.NotFound);
        }

        if (cheque.Version != command.Version)
        {
            return Result.Failure<ChequeResponse>(ChequeErrors.ChangedConcurrently);
        }

        var updated = cheque.Update(command.Amount, command.DueDate, command.Payee, command.Description);
        if (updated.IsFailure)
        {
            return Result.Failure<ChequeResponse>(updated.Error);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ChequeResponse>(ChequeErrors.ChangedConcurrently);
        }

        return ChequeResponse.From(cheque);
    }
}
