using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Pricing;
using Gym.Application.Pricing.GetPrices;
using Gym.Application.Pricing.UpdatePrices;

namespace Gym.Api.Endpoints;

/// <summary>
/// The gym's two prices (BUSINESS_RULES.md §3 <i>Prices</i>). Setting them is the Owner's job; the
/// desk sells at them, so both roles can read them (§1, permissions).
/// </summary>
/// <remarks>
/// Each endpoint names its own policy rather than inheriting one from the group, so a new endpoint
/// here cannot quietly get the wrong one (the lesson of task 6.5.1).
/// </remarks>
public static class PricingEndpoints
{
    public static IEndpointRouteBuilder MapPricingEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var pricing = app.MapGroup("/api/pricing")
            .WithTags("Pricing")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        pricing.MapGet("/", async (GetPricesHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(ct)))
            .RequireAuthorization(Policies.StaffOrOwner)
            .WithName("GetPrices")
            .Produces<PricesResponse>();

        pricing.MapPut("/", async (UpdatePricesCommand command, UpdatePricesHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttpResult())
            .RequireAuthorization(Policies.OwnerOnly)
            .AddEndpointFilter<ValidationFilter<UpdatePricesCommand>>()
            .WithName("UpdatePrices")
            .Produces<PricesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
