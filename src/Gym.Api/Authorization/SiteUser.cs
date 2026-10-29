using System.Security.Claims;

using Gym.Infrastructure.SiteAuth;

namespace Gym.Api.Authorization;

/// <summary>The site account a request's token was issued for (ADR 0010).</summary>
public static class SiteUser
{
    /// <summary>
    /// The site account's id. Every site policy has already required the claim, so a token without
    /// one never reaches an endpoint that asks.
    /// </summary>
    public static Guid AccountId(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return Guid.Parse(user.FindFirst(SiteClaimNames.SiteAccount)!.Value);
    }
}
