using Gym.Application.Common;
using Gym.Application.Common.Security;
using Gym.Domain.Common;

namespace Gym.Application.Auth.GetCurrentUser;

/// <summary>
/// The logged-in user as the database knows them now, not as the access token remembers them:
/// a role change or deactivation shows up here at once, not 15 minutes later.
/// </summary>
public sealed class GetCurrentUserHandler(IUserAuthenticator users, ICurrentUser currentUser)
{
    public async Task<Result<CurrentUserResponse>> Handle(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Get current user was called without an authenticated user.");

        var user = await users.GetActiveUserAsync(userId, cancellationToken);
        if (user.IsFailure)
        {
            return Result.Failure<CurrentUserResponse>(user.Error);
        }

        var value = user.Value;

        return new CurrentUserResponse(value.Id, value.UserName, value.FullName, value.Roles, value.MustChangePassword);
    }
}
