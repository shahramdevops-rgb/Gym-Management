using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Configuration;
using Gym.Api.Filters;
using Gym.Application.Auth;
using Gym.Application.Auth.ChangePassword;
using Gym.Application.Auth.GetCurrentUser;
using Gym.Application.Auth.Login;
using Gym.Application.Auth.Logout;
using Gym.Application.Auth.Refresh;
using Gym.Domain.Common;

namespace Gym.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/auth").WithTags("Auth");

        // Anonymous by necessity: this is how a caller gets a token in the first place. It is
        // the only endpoint that checks a password, which is why it carries the rate limit.
        group.MapPost("/login", async (LoginCommand command, LoginHandler handler, HttpResponse response, CancellationToken ct) =>
                ToSessionResult(await handler.Handle(command, ct), response))
            .AddEndpointFilter<ValidationFilter<LoginCommand>>()
            .RequireRateLimiting(RateLimitingConfiguration.LoginPolicy)
            .AllowAnonymous()
            .WithName("Login")
            .Produces<AccessTokenResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // Anonymous because the access token has usually expired by the time this is called;
        // the refresh cookie is the credential here.
        group.MapPost("/refresh", async (RefreshHandler handler, HttpRequest request, HttpResponse response, CancellationToken ct) =>
                ToSessionResult(await handler.Handle(new RefreshCommand(RefreshTokenCookie.Read(request)), ct), response))
            .AllowAnonymous()
            .WithName("Refresh")
            .Produces<AccessTokenResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // Anonymous for the same reason, and because a user with MustChangePassword may always
        // log out (BUSINESS_RULES.md §1). Clears the cookie whatever state it was in.
        group.MapPost("/logout", async (LogoutHandler handler, HttpRequest request, HttpResponse response, CancellationToken ct) =>
            {
                var result = await handler.Handle(new LogoutCommand(RefreshTokenCookie.Read(request)), ct);
                RefreshTokenCookie.Clear(response);

                return result.ToHttpResult();
            })
            .AllowAnonymous()
            .WithName("Logout")
            .Produces(StatusCodes.Status204NoContent);

        // The one endpoint a user with a temporary password may call besides logout, which is
        // why it uses PasswordChangeAllowed, the only policy without the gate. Rate-limited like
        // login because it checks a password too.
        group.MapPost("/change-password", async (ChangePasswordCommand command, ChangePasswordHandler handler, HttpResponse response, CancellationToken ct) =>
                ToSessionResult(await handler.Handle(command, ct), response))
            .AddEndpointFilter<ValidationFilter<ChangePasswordCommand>>()
            .RequireRateLimiting(RateLimitingConfiguration.LoginPolicy)
            .RequireAuthorization(Policies.PasswordChangeAllowed)
            .WithName("ChangePassword")
            .Produces<AccessTokenResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/me", async (GetCurrentUserHandler handler, CancellationToken ct) =>
                (await handler.Handle(ct)).ToHttpResult())
            .RequireAuthorization(Policies.StaffOrOwner)
            .WithName("GetCurrentUser")
            .Produces<CurrentUserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    /// <summary>
    /// Success puts the new refresh token in the cookie and the access token in the body.
    /// Failure clears the cookie: a refresh token that was just refused is worthless, and
    /// leaving it would only make the browser send it again.
    /// </summary>
    private static IResult ToSessionResult(Result<AuthSession> result, HttpResponse response)
    {
        if (result.IsFailure)
        {
            RefreshTokenCookie.Clear(response);
        }

        return result.ToHttpResult(session =>
        {
            RefreshTokenCookie.Write(response, session.RefreshToken, session.RefreshTokenExpiresAt);

            return Results.Ok(session.Response);
        });
    }
}
