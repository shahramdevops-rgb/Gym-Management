using Gym.Application.Common;
using Gym.Domain.Cheques;
using Gym.Domain.Common;

namespace Gym.Application.Cheques.RegisterCheque;

/// <summary>Writes a cheque the gym gave into the register (BUSINESS_RULES.md §9 <i>Cheques</i>). Owner only.</summary>
public sealed class RegisterChequeHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<ChequeResponse>> Handle(RegisterChequeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // The endpoint's policy requires an authenticated user, so this is a wiring bug if hit.
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Registering a cheque was called without an authenticated user.");

        var registered = Cheque.Register(command.Amount, command.DueDate, command.Payee, command.Description, userId);
        if (registered.IsFailure)
        {
            return Result.Failure<ChequeResponse>(registered.Error);
        }

        var cheque = registered.Value;
        db.Cheques.Add(cheque);
        await db.SaveChangesAsync(cancellationToken);

        return ChequeResponse.From(cheque);
    }
}
