using Gym.Application.Common;
using Gym.Domain.Cheques;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Cheques.MarkChequePassed;

/// <summary>
/// «پاس شد»: the money has left the account, so the cheque leaves the reminder (BUSINESS_RULES.md
/// §9 <i>Cheques</i>). Owner only.
/// </summary>
/// <remarks>
/// No expense is written: on the cheque's date the Owner records it by hand (§9), and nothing links
/// the two. <c>xmin</c> alone settles a mark racing an edit or a cancel.
/// </remarks>
public sealed class MarkChequePassedHandler(
    IAppDbContext db, IGymCalendar calendar, TimeProvider time, ICurrentUser currentUser)
{
    public async Task<Result<ChequeResponse>> Handle(Guid id, CancellationToken cancellationToken)
    {
        var cheque = await db.Cheques.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (cheque is null)
        {
            return Result.Failure<ChequeResponse>(ChequeErrors.NotFound);
        }

        // The endpoint's policy requires an authenticated user, so this is a wiring bug if hit.
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Marking a cheque passed was called without an authenticated user.");

        var passed = cheque.MarkPassed(calendar.Today(), time.GetUtcNow(), userId);
        if (passed.IsFailure)
        {
            return Result.Failure<ChequeResponse>(passed.Error);
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
