using Gym.Application.Common.Security;
using Gym.Domain.Common;

namespace Gym.Application.Auth.Login;

/// <summary>
/// Exchanges a user name and password for an access token.
/// </summary>
/// <remarks>
/// Short on purpose. The password check, lockout and active flag are Identity's territory and
/// sit behind <see cref="IUserAuthenticator"/>; the token format sits behind
/// <see cref="IAccessTokenIssuer"/>. What remains here is the use case itself: no token without
/// a successful authentication.
/// </remarks>
public sealed class LoginHandler(IUserAuthenticator authenticator, IAccessTokenIssuer tokenIssuer)
{
    public async Task<Result<LoginResponse>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var authentication = await authenticator.AuthenticateAsync(
            command.UserName,
            command.Password,
            cancellationToken);

        if (authentication.IsFailure)
        {
            return Result.Failure<LoginResponse>(authentication.Error);
        }

        var user = authentication.Value;
        var token = tokenIssuer.Issue(user);

        return new LoginResponse(token.Value, token.ExpiresAt, user.MustChangePassword);
    }
}
