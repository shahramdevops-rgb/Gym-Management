using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Payables;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Payables.MarkPayablePaid;

/// <summary>
/// «پاس شد» / «پرداخت شد»: the money has left the account, so the record leaves the reminder and
/// its expense is recorded, dated today (BUSINESS_RULES.md §9 <i>Cheques and instalments</i>).
/// Owner only.
/// </summary>
/// <remarks>
/// The mark and the expense are one <c>SaveChanges</c>, so one transaction: there is never a paid
/// record without its expense, or the other way round. Two marks racing are settled by <c>xmin</c>
/// on the payable and, whichever statement runs first, by the partial unique index on the expense.
/// </remarks>
public sealed class MarkPayablePaidHandler(
    IAppDbContext db, IGymCalendar calendar, TimeProvider time, ICurrentUser currentUser)
{
    public async Task<Result<PayableResponse>> Handle(Guid id, CancellationToken cancellationToken)
    {
        var payable = await db.Payables.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (payable is null)
        {
            return Result.Failure<PayableResponse>(PayableErrors.NotFound);
        }

        // The endpoint's policy requires an authenticated user, so this is a wiring bug if hit.
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Marking a cheque or instalment paid was called without an authenticated user.");

        var paid = payable.MarkPaid(calendar.Today(), time.GetUtcNow(), userId);
        if (paid.IsFailure)
        {
            return Result.Failure<PayableResponse>(paid.Error);
        }

        db.Expenses.Add(paid.Value);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<PayableResponse>(PayableErrors.ChangedConcurrently);
        }
        catch (UniqueConstraintException exception)
            when (exception.ConstraintName == PayableConstraints.OneStandingExpense)
        {
            return Result.Failure<PayableResponse>(PayableErrors.ChangedConcurrently);
        }

        return await PayableResponses.ForAsync(db, payable, cancellationToken);
    }
}
