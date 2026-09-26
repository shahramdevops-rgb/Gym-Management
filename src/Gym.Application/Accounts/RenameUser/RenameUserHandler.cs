using Gym.Application.Common.Security;
using Gym.Domain.Common;

namespace Gym.Application.Accounts.RenameUser;

/// <summary>
/// <c>./server.sh rename &lt;old&gt; &lt;new&gt;</c>: a new user name for any account, the Owner's
/// included. A guessable name like <c>Owner</c> tells an attacker half of the login.
/// </summary>
/// <remarks>
/// Sessions stay: a refresh reloads the user, so the next access token already carries the new
/// name. The person logs in with the new name from then on.
/// </remarks>
public sealed class RenameUserHandler(IUserAccounts accounts)
{
    public async Task<Result> Handle(string userName, string newUserName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(newUserName);

        var name = newUserName.Trim();
        if (name.Length is < UserNamePolicy.MinimumLength or > UserNamePolicy.MaximumLength ||
            !UserNamePolicy.HasOnlyAllowedCharacters(name))
        {
            return Result.Failure(AccountErrors.UserNameInvalid);
        }

        if (UserNamePolicy.IsGuessable(name))
        {
            return Result.Failure(AccountErrors.UserNameGuessable);
        }

        var userId = await accounts.FindIdByUserNameAsync(userName, cancellationToken);
        if (userId is null)
        {
            return Result.Failure(AccountErrors.UserNotFound);
        }

        return await accounts.RenameAsync(userId.Value, name, cancellationToken);
    }
}
