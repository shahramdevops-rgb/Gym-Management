using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Shop;
using Gym.Application.Shop.GetShopContacts;
using Gym.Application.Shop.UpdateShopContacts;

namespace Gym.Api.Endpoints;

/// <summary>
/// The online shop's panel (ADR 0011, BUSINESS_RULES.md §16). The shop admin changes it
/// (<see cref="Policies.ShopAdmin"/>); the Owner reads it (<see cref="Policies.ShopOrOwner"/>); Staff
/// never reach it.
/// </summary>
public static class ShopEndpoints
{
    public static IEndpointRouteBuilder MapShopEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // No policy on the group: each endpoint names its own, reading or changing, so neither can
        // be added without deciding which.
        var shop = app.MapGroup("/api/shop")
            .WithTags("Shop")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        shop.MapGet("/contacts", async (GetShopContactsHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(ct)))
            .RequireAuthorization(Policies.ShopOrOwner)
            .WithName("GetShopContacts")
            .Produces<ShopContactsResponse>();

        shop.MapPut("/contacts", async (UpdateShopContactsCommand command, UpdateShopContactsHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<UpdateShopContactsCommand>>()
            .RequireAuthorization(Policies.ShopAdmin)
            .WithName("UpdateShopContacts")
            .Produces<ShopContactsResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
