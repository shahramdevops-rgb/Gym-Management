using Gym.Api.Common;
using Gym.Api.Configuration;
using Gym.Api.Filters;
using Gym.Application.Auth.Login;

namespace Gym.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/auth").WithTags("Auth");

        // Anonymous by necessity: this is how a caller gets a token in the first place. It is
        // the only endpoint an unauthenticated caller can reach, which is why it carries the
        // rate limit.
        group.MapPost("/login", async (LoginCommand command, LoginHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<LoginCommand>>()
            .RequireRateLimiting(RateLimitingConfiguration.LoginPolicy)
            .AllowAnonymous()
            .WithName("Login")
            .Produces<LoginResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return app;
    }
}
