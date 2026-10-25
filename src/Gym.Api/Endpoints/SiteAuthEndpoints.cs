using Gym.Api.Common;
using Gym.Api.Configuration;
using Gym.Api.Filters;
using Gym.Application.SiteAuth.RequestCode;

namespace Gym.Api.Endpoints;

/// <summary>
/// The site's sign-in (BUSINESS_RULES.md §15, ADR 0010), for members and trainers on
/// <c>app.pasargadgymplus.ir</c>. Apart from the staff's <c>/api/auth</c> in everything: its own
/// path, its own rate limits and, from task 15.1b, its own tokens.
/// </summary>
public static class SiteAuthEndpoints
{
    public static IEndpointRouteBuilder MapSiteAuthEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/site/auth").WithTags("Site sign-in");

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

        return app;
    }
}
