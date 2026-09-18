using Gym.Application.Common;
using Gym.Application.Common.Security;
using Gym.Application.Common.Text;
using Gym.Domain.Auth;
using Gym.Domain.Common;

namespace Gym.Application.Auth.Login;

/// <summary>
/// Exchanges a user name and password for an access token and a refresh token.
/// </summary>
/// <remarks>
/// The password check, lockout and active flag are Identity's territory and sit behind
/// <see cref="IUserAuthenticator"/>; the token format sits behind <see cref="IAccessTokenIssuer"/>.
/// What remains here is the use case itself: no tokens without a successful authentication,
/// and every login starts a new refresh token family.
/// </remarks>
public sealed class LoginHandler(
    IUserAuthenticator authenticator,
    IAccessTokenIssuer tokenIssuer,
    IAppDbContext db,
    TimeProvider timeProvider)
{
    public async Task<Result<AuthSession>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var authentication = await authenticator.AuthenticateAsync(
            command.UserName,
            Digits.ToEnglish(command.Password),
            cancellationToken);

        if (authentication.IsFailure)
        {
            return Result.Failure<AuthSession>(authentication.Error);
        }

        var user = authentication.Value;

        var refreshSecret = RefreshTokenSecret.Generate();
        var refreshToken = RefreshToken.Issue(user.Id, RefreshTokenSecret.Hash(refreshSecret), timeProvider.GetUtcNow());
        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync(cancellationToken);

        return AuthSession.Create(user, tokenIssuer.Issue(user), refreshSecret, refreshToken.ExpiresAt);
    }
}
