namespace Gym.Application.Common.Security;

/// <summary>Creates the short-lived access token a client sends with every request.</summary>
/// <remarks>
/// An interface because the token format (JWT, its signing key, its claim names) is an
/// infrastructure detail. The use case only needs "a token for this user, and when it expires".
/// </remarks>
public interface IAccessTokenIssuer
{
    AccessToken Issue(AuthenticatedUser user);
}
