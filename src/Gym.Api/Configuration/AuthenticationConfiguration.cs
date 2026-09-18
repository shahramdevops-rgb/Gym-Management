using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Application.Common;
using Gym.Infrastructure.Identity;

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

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configured from JwtOptions when first needed rather than here, so the bearer handler
        // sees the final configuration (user-secrets, environment variables, a test host's
        // settings) and uses exactly the rules JwtAccessTokenIssuer signs for.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                bearer.TokenValidationParameters = jwt.Value.CreateValidationParameters();

                // Keep claim names as written ("sub", "role") instead of rewriting them to the
                // long XML-namespace URIs .NET maps them to by default.
                bearer.MapInboundClaims = false;
            });

        // The fallback policy applies to every endpoint that declares no policy of its own.
        // CLAUDE.md already requires an explicit policy on each endpoint; this makes forgetting
        // one fail closed (401) instead of open. Anonymous endpoints must say AllowAnonymous().
        // Every policy but PasswordChangeAllowed carries the forced password change gate, the
        // fallback included, so a user with a temporary password is refused everywhere by
        // default rather than by each endpoint remembering to check.
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PasswordChangedRequirement())
                .Build())
            .AddPolicy(Policies.OwnerOnly, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(Roles.Owner)
                .AddRequirements(new PasswordChangedRequirement()))
            .AddPolicy(Policies.StaffOrOwner, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(Roles.Staff, Roles.Owner)
                .AddRequirements(new PasswordChangedRequirement()))
            .AddPolicy(Policies.PasswordChangeAllowed, policy => policy
                .RequireAuthenticatedUser());

        services.AddSingleton<IAuthorizationHandler, PasswordChangedHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ProblemDetailsAuthorizationResultHandler>();

        // The Api's answer to "who is making this request", for Application and Infrastructure.
        services.AddHttpContextAccessor();
        services.AddSingleton<ICurrentUser, HttpContextCurrentUser>();

        return services;
    }
}
