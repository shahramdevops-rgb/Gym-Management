using System.Globalization;
using System.Security.Claims;

using Gym.Application.Common.Security;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Gym.Infrastructure.Identity;

/// <summary>
/// Signs a JWT access token that expires <see cref="JwtOptions.AccessTokenLifetime"/> after it is issued.
/// </summary>
/// <remarks>
/// "Now" comes from <see cref="TimeProvider"/>, never from the system clock, so a test can
/// issue a token in the past and prove that validation rejects it once it has expired.
/// </remarks>
public sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider timeProvider)
    : IAccessTokenIssuer
{
    // Stateless and thread-safe, so one instance serves every request.
    private static readonly JsonWebTokenHandler TokenHandler = new();

    public AccessToken Issue(AuthenticatedUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var jwt = options.Value;
        var issuedAt = timeProvider.GetUtcNow();
        var expiresAt = issuedAt.Add(JwtOptions.AccessTokenLifetime);

        var claims = new List<Claim>
        {
            new(JwtClaimNames.Subject, user.Id.ToString()),
            new(JwtClaimNames.TokenId, Guid.CreateVersion7().ToString()),
            new(JwtClaimNames.Name, user.UserName),
            new(JwtClaimNames.FullName, user.FullName),
            new(JwtClaimNames.MustChangePassword, user.MustChangePassword.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()),
        };
        claims.AddRange(user.Roles.Select(role => new Claim(JwtClaimNames.Role, role)));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(jwt.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
        };

        return new AccessToken(TokenHandler.CreateToken(descriptor), expiresAt);
    }
}
