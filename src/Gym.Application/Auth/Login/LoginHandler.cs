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
    /// <param name="deviceToken">The request's <c>gym_device</c> cookie, if it has one.</param>
    public async Task<Result<AuthSession>> Handle(LoginCommand command, string? deviceToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var password = PersianText.NormalizeDigits(command.Password);
        var authentication = await authenticator.AuthenticateAsync(
            command.UserName,
            password,
            TrustedDevices.HashOf(deviceToken),
            cancellationToken);

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

        var now = timeProvider.GetUtcNow();

        var refreshSecret = RefreshTokenSecret.Generate();
        var refreshToken = RefreshToken.Issue(user.Id, RefreshTokenSecret.Hash(refreshSecret), now);
        db.RefreshTokens.Add(refreshToken);

        // From now on, wrong passwords typed on this browser lock only this browser
        // (BUSINESS_RULES.md §1 *Lockout*).
        var trustedDeviceToken = await db.TrustDeviceAsync(user.Id, deviceToken, now, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return AuthSession.Create(user, tokenIssuer.Issue(user), refreshSecret, refreshToken.ExpiresAt) with
        {
            DeviceToken = trustedDeviceToken,
            DeviceTokenExpiresAt = now + TrustedDevice.Lifetime,
        };
    }
}
