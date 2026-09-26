using Gym.Domain.Common;

namespace Gym.Application.Accounts.UnlockUser;

/// <summary><c>./server.sh unlock &lt;user&gt;</c>: clears the lockout of any account, the Owner's included.</summary>
public sealed class UnlockUserHandler(IUserAccounts accounts)
{
    public async Task<Result> Handle(string userName, CancellationToken cancellationToken)
    {
        var userId = await accounts.FindIdByUserNameAsync(userName, cancellationToken);
        if (userId is null)
        {
            return Result.Failure(AccountErrors.UserNotFound);
        }

        return await accounts.UnlockAsync(userId.Value, cancellationToken);
    }
}
