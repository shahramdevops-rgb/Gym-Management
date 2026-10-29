using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Gym.Api.Authorization;

/// <summary>
/// The two ways a request can be signed in (ADR 0010). Every policy names exactly one of them, so a
/// token of one kind can never satisfy a policy of the other, even if a role or a claim were ever
/// written wrong.
/// </summary>
public static class AuthSchemes
{
    /// <summary>The panel's staff: Identity users, the <c>Jwt</c> section's key. The default scheme.</summary>
    public const string Staff = JwtBearerDefaults.AuthenticationScheme;

    /// <summary>Members and trainers on the site: site accounts, the <c>SiteAuth</c> section's key.</summary>
    public const string Site = "Site";
}

/// <summary>Where the site's endpoints live: everything under it is read with <see cref="AuthSchemes.Site"/> only.</summary>
public static class SitePaths
{
    public const string Root = "/api/site";
}
