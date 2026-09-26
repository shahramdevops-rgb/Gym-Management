using Gym.Application.Common.Security;
using Gym.Application.Common;
using Gym.Domain.Auth;
using Gym.Domain.Common.Text;
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

        var password = PersianText.NormalizeDigits(command.Password);
        var authentication = await authenticator.AuthenticateAsync(command.UserName, password, cancellationToken);

        if (authentication.IsFailure)
        {
            return Result.Failure<AuthSession>(authentication.Error);
        }

        var user = authentication.Value;

        // BUSINESS_RULES.md §1: a password set under an older, weaker policy still logs in, but
        // must be replaced before anything else. Login is the only moment the server sees the
        // password itself rather than its hash, so it is the only place this can be checked.
        if (!user.MustChangePassword && PasswordPolicy.Check(password, user.UserName).IsFailure)
        {
            await authenticator.RequirePasswordChangeAsync(user.Id, cancellationToken);
            user = user with { MustChangePassword = true };
        }

        var refreshSecret = RefreshTokenSecret.Generate();
        var refreshToken = RefreshToken.Issue(user.Id, RefreshTokenSecret.Hash(refreshSecret), timeProvider.GetUtcNow());
        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync(cancellationToken);

        return AuthSession.Create(user, tokenIssuer.Issue(user), refreshSecret, refreshToken.ExpiresAt);
    }
}
