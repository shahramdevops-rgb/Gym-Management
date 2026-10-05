using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Payables;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Payables.RegisterPayable;

/// <summary>
/// Writes a cheque the gym gave, or one instalment, into the register (BUSINESS_RULES.md §9
/// <i>Cheques and instalments</i>). Owner only.
/// </summary>
public sealed class RegisterPayableHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<PayableResponse>> Handle(RegisterPayableCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // The endpoint's policy requires an authenticated user, so this is a wiring bug if hit.
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Registering a cheque or instalment was called without an authenticated user.");

        var registered = Payable.Register(
            command.Kind,
            command.Amount,
            command.DueDate,
            command.Payee,
            command.Description,
            command.CategoryId,
            command.InstallmentNumber,
            command.InstallmentCount,
            userId);
        if (registered.IsFailure)
        {
            return Result.Failure<PayableResponse>(registered.Error);
        }

        var category = await db.ExpenseCategories.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == command.CategoryId, cancellationToken);
        if (category is null)
        {
            return Result.Failure<PayableResponse>(PayableErrors.CategoryNotFound);
        }

        var payable = registered.Value;
        db.Payables.Add(payable);
        await db.SaveChangesAsync(cancellationToken);

        return PayableResponse.From(payable, category.Name, expenseId: null);
    }
}
