using System.Security.Claims;

using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Configuration;
using Gym.Api.Filters;
using Gym.Application.SiteAuth;
using Gym.Application.SiteAuth.GetMe;
using Gym.Application.SiteAuth.Logout;
using Gym.Application.SiteAuth.Refresh;
using Gym.Application.SiteAuth.RequestCode;
using Gym.Application.SiteAuth.SignIn;
using Gym.Application.SiteAuth.SignUp;
using Gym.Domain.Common;
using Gym.Infrastructure.SiteAuth;

namespace Gym.Api.Endpoints;

/// <summary>
/// The site's sign-in (BUSINESS_RULES.md §15, ADR 0010), for members on <c>app.pasargadgymplus.ir</c>.
/// Apart from the staff's <c>/api/auth</c> in everything: its own path, rate limits, tokens and cookie.
/// </summary>
public static class SiteAuthEndpoints
{
    public static IEndpointRouteBuilder MapSiteAuthEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup($"{SitePaths.Root}/auth").WithTags("Site sign-in");

        // Anonymous by necessity: a phone asks for its code before it is anyone. Every code is a
        // paid SMS, so the address is limited here and the phone and the day in the handler.
        group.MapPost("/code", async (RequestCodeCommand command, RequestCodeHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<RequestCodeCommand>>()
            .RequireRateLimiting(RateLimitingConfiguration.SiteCodePolicy)
            .AllowAnonymous()
            .WithName("RequestSiteCode")
            .Produces<RequestCodeResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // Anonymous: the code is the credential. A member's phone gets its session (the refresh
        // token in the cookie); a new phone gets the ticket for the sign-up screen.
        group.MapPost("/sign-in", async (SignInCommand command, SignInHandler handler, HttpResponse response, CancellationToken ct) =>
            {
                var result = await handler.Handle(command, ct);

                return result.ToHttpResult(signIn =>
                {
                    if (signIn.Session is { } session)
                    {
                        SiteRefreshTokenCookie.Write(response, session.RefreshToken, session.RefreshTokenExpiresAt);
                    }

                    return Results.Ok(signIn.Response);
                });
            })
            .AddEndpointFilter<ValidationFilter<SignInCommand>>()
            .RequireRateLimiting(RateLimitingConfiguration.SiteSignInPolicy)
            .AllowAnonymous()
            .WithName("SiteSignIn")
            .Produces<SignInResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // Anonymous: the sign-up ticket is the credential, and proves the phone.
        group.MapPost("/sign-up", async (SignUpCommand command, SignUpHandler handler, HttpResponse response, CancellationToken ct) =>
                ToSessionResult(await handler.Handle(command, ct), response))
            .AddEndpointFilter<ValidationFilter<SignUpCommand>>()
            .RequireRateLimiting(RateLimitingConfiguration.SiteSignInPolicy)
            .AllowAnonymous()
            .WithName("SiteSignUp")
            .Produces<SiteAccessResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // Anonymous because the access token has usually expired by then: the cookie is the credential.
        group.MapPost("/refresh", async (SiteRefreshHandler handler, HttpRequest request, HttpResponse response, CancellationToken ct) =>
                ToSessionResult(await handler.Handle(SiteRefreshTokenCookie.Read(request), ct), response))
            .RequireRateLimiting(RateLimitingConfiguration.SiteSessionPolicy)
            .AllowAnonymous()
            .WithName("SiteRefresh")
            .Produces<SiteAccessResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/logout", async (SiteLogoutHandler handler, HttpRequest request, HttpResponse response, CancellationToken ct) =>
            {
                var result = await handler.Handle(SiteRefreshTokenCookie.Read(request), ct);
                SiteRefreshTokenCookie.Clear(response);

                return result.ToHttpResult();
            })
            .RequireRateLimiting(RateLimitingConfiguration.SiteSessionPolicy)
            .AllowAnonymous()
            .WithName("SiteLogout")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // The first endpoint behind the site's own policy: a staff token never passes it.
        app.MapGet($"{SitePaths.Root}/me", async (ClaimsPrincipal user, GetSiteMeHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteAccountId(user), ct)).ToHttpResult())
            .WithTags("Site sign-in")
            .RequireAuthorization(Policies.SiteAccount)
            .WithName("GetSiteMe")
            .Produces<SiteMeResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    /// <summary>
    /// The account the site's token was issued for. The site's policy has already required the
    /// claim, so a token without one never reaches an endpoint.
    /// </summary>
    private static Guid SiteAccountId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirst(SiteClaimNames.SiteAccount)!.Value);

    /// <summary>
    /// Success puts the refresh token in the site's cookie and the access token in the body;
    /// failure clears the cookie, as the staff's sign-in does.
    /// </summary>
    private static IResult ToSessionResult(Result<SiteSession> result, HttpResponse response)
    {
        if (result.IsFailure)
        {
            SiteRefreshTokenCookie.Clear(response);
        }

        return result.ToHttpResult(session =>
        {
            SiteRefreshTokenCookie.Write(response, session.RefreshToken, session.RefreshTokenExpiresAt);

            return Results.Ok(session.Response);
        });
    }
}
