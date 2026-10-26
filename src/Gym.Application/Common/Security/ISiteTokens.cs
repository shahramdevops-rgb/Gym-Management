using Gym.Domain.SiteAuth;

namespace Gym.Application.Common.Security;

/// <summary>
/// The site's own tokens (ADR 0010): signed with the site's key, for the site's issuer and
/// audience, so none of them can pass for a staff token, nor a staff token for one of these.
/// </summary>
public interface ISiteTokens
{
    /// <summary>The short-lived access token the site app sends with every request.</summary>
    AccessToken IssueAccessToken(SiteAccount account);

    /// <summary>
    /// Proof, for 15 minutes, that this phone has just typed its code: what a new phone signs up
    /// with (BUSINESS_RULES.md §15). It is not an access token and opens no endpoint.
    /// </summary>
    AccessToken IssueSignUpTicket(string phoneNumber);

    /// <summary>The phone a valid, unexpired ticket was issued for; <c>null</c> for anything else.</summary>
    Task<string?> ReadSignUpTicketAsync(string ticket);
}
