using System.Security.Claims;

using Gym.Application.Common.Security;
using Gym.Application.SiteAuth;
using Gym.Domain.SiteAuth;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Gym.Infrastructure.SiteAuth;

/// <summary>
/// The site's tokens as JWTs, signed with <c>SiteAuth:SigningKey</c> for the site's issuer: access
/// tokens for the site app's audience, sign-up tickets for another, so neither works as the other.
/// </summary>
public sealed class JwtSiteTokens(IOptions<SiteAuthOptions> options, TimeProvider timeProvider) : ISiteTokens
{
    // Stateless and thread-safe, so one instance serves every request.
    private static readonly JsonWebTokenHandler TokenHandler = new() { MapInboundClaims = false };

    public AccessToken IssueAccessToken(SiteAccount account, SitePerson person)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(person);

        List<Claim> claims = [new(SiteClaimNames.SiteAccount, account.Id.ToString())];
        if (person.IsMember)
        {
            claims.Add(new Claim(SiteClaimNames.Role, SiteRoles.Member));
        }

        if (person.IsTrainer)
        {
            claims.Add(new Claim(SiteClaimNames.Role, SiteRoles.Trainer));
        }

        return Issue(options.Value.Audience, SiteAuthOptions.AccessTokenLifetime, [.. claims]);
    }

    public AccessToken IssueSignUpTicket(string phoneNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);

        return Issue(options.Value.SignUpAudience, SiteAuthOptions.SignUpTicketLifetime, [new Claim(SiteClaimNames.Phone, phoneNumber)]);
    }

    public async Task<string?> ReadSignUpTicketAsync(string ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket))
        {
            return null;
        }

        var parameters = options.Value.CreateValidationParameters(options.Value.SignUpAudience);

        // The library checks the lifetime against the clock it is given, and this API's clock is
        // the TimeProvider (CLAUDE.md), so a test can let a ticket run out.
        parameters.LifetimeValidator = (notBefore, expires, _, _) =>
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;

            return (notBefore is null || notBefore <= now) && expires is not null && now < expires;
        };

        var result = await TokenHandler.ValidateTokenAsync(ticket, parameters);
        if (!result.IsValid)
        {
            return null;
        }

        return result.ClaimsIdentity.FindFirst(SiteClaimNames.Phone)?.Value;
    }

    private AccessToken Issue(string audience, TimeSpan lifetime, Claim[] claims)
    {
        var site = options.Value;
        var issuedAt = timeProvider.GetUtcNow();
        var expiresAt = issuedAt.Add(lifetime);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = site.Issuer,
            Audience = audience,
            Subject = new ClaimsIdentity([.. claims, new Claim(SiteClaimNames.TokenId, Guid.CreateVersion7().ToString())]),
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(site.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
        };

        return new AccessToken(TokenHandler.CreateToken(descriptor), expiresAt);
    }
}
