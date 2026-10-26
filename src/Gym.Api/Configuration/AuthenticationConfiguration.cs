using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Application.Common;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.SiteAuth;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Gym.Api.Configuration;

/// <summary>
/// JWT bearer authentication, plus the rule that every endpoint needs an authenticated user
/// unless it says otherwise.
/// </summary>
public static class AuthenticationConfiguration
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Two schemes, never mixed (ADR 0010). The staff's stays the default, so every request is
        // read as a staff request first; the site's is used only by the site's policies.
        services.AddAuthentication(AuthSchemes.Staff)
            .AddJwtBearer(AuthSchemes.Staff)
            .AddJwtBearer(AuthSchemes.Site);

        // Configured from JwtOptions when first needed rather than here, so the bearer handler
        // sees the final configuration (user-secrets, environment variables, a test host's
        // settings) and uses exactly the rules JwtAccessTokenIssuer signs for.
        services.AddOptions<JwtBearerOptions>(AuthSchemes.Staff)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                bearer.TokenValidationParameters = jwt.Value.CreateValidationParameters();

                // Keep claim names as written ("sub", "role") instead of rewriting them to the
                // long XML-namespace URIs .NET maps them to by default.
                bearer.MapInboundClaims = false;

                // Requests to the site's own endpoints are read with the site's scheme only, the
                // anonymous ones (sign-in, sign-up) included: a staff token sent there is never
                // read, so nothing done there is recorded as that staff user (ADR 0010).
                bearer.ForwardDefaultSelector = context =>
                    context.Request.Path.StartsWithSegments(SitePaths.Root) ? AuthSchemes.Site : null;
            });

        // The site's issuer, audience and key (SiteAuthOptions): a staff token fails here, and a
        // site token fails the staff's scheme above.
        services.AddOptions<JwtBearerOptions>(AuthSchemes.Site)
            .Configure<IOptions<SiteAuthOptions>>((bearer, site) =>
            {
                bearer.TokenValidationParameters = site.Value.CreateValidationParameters();
                bearer.MapInboundClaims = false;

                // Marks the identity as the site's, which HttpContextCurrentUser refuses to read
                // as a staff user whatever claims it carries.
                bearer.TokenValidationParameters.AuthenticationType = AuthSchemes.Site;
            });

        // The fallback policy applies to every endpoint that declares no policy of its own.
        // CLAUDE.md already requires an explicit policy on each endpoint; this makes forgetting
        // one fail closed (401) instead of open. Anonymous endpoints must say AllowAnonymous().
        // Every policy but PasswordChangeAllowed carries the forced password change gate, the
        // fallback included, so a user with a temporary password is refused everywhere by
        // default rather than by each endpoint remembering to check.
        //
        // Every staff policy, the fallback included, names the staff scheme, and the site's names
        // the site scheme (ADR 0010): which key signed the token is checked by the policy itself,
        // not left to the default scheme.
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder(AuthSchemes.Staff)
                .RequireAuthenticatedUser()
                .AddRequirements(new PasswordChangedRequirement())
                .Build())
            .AddPolicy(Policies.OwnerOnly, policy => policy
                .AddAuthenticationSchemes(AuthSchemes.Staff)
                .RequireAuthenticatedUser()
                .RequireRole(Roles.Owner)
                .AddRequirements(new PasswordChangedRequirement()))
            .AddPolicy(Policies.StaffOrOwner, policy => policy
                .AddAuthenticationSchemes(AuthSchemes.Staff)
                .RequireAuthenticatedUser()
                .RequireRole(Roles.Staff, Roles.Owner)
                .AddRequirements(new PasswordChangedRequirement()))
            .AddPolicy(Policies.PasswordChangeAllowed, policy => policy
                .AddAuthenticationSchemes(AuthSchemes.Staff)
                .RequireAuthenticatedUser())
            .AddPolicy(Policies.SiteAccount, policy => policy
                .AddAuthenticationSchemes(AuthSchemes.Site)
                .RequireAuthenticatedUser()
                .RequireClaim(SiteClaimNames.SiteAccount));

        services.AddSingleton<IAuthorizationHandler, PasswordChangedHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ProblemDetailsAuthorizationResultHandler>();

        // The Api's answer to "who is making this request", for Application and Infrastructure.
        services.AddHttpContextAccessor();
        services.AddSingleton<ICurrentUser, HttpContextCurrentUser>();

        return services;
    }
}
