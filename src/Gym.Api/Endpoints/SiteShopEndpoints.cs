using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Configuration;
using Gym.Application.Shop;
using Gym.Application.Shop.GetShowcase;
using Gym.Application.Shop.GetShowcasePhoto;
using Gym.Application.Shop.GetShowcaseProduct;

using Microsoft.Net.Http.Headers;

namespace Gym.Api.Endpoints;

/// <summary>
/// The shop's showcase on the gym's site (roadmap 16.4, BUSINESS_RULES.md §16 <i>The showcase</i>):
/// the shown products, one product's page and the product photos, for anyone, without signing in.
/// </summary>
/// <remarks>
/// <para>
/// <b>Under <c>/api/site/shop</c>, and nothing else of the API.</b> The gym's public address
/// (<c>pasargadgymplus.ir</c>) hands exactly this path to the API (deploy/Caddyfile); the rest of
/// <c>/api</c> is not reachable from it. The shop admin's own endpoints stay on the panel's address
/// (<see cref="ShopEndpoints"/>).
/// </para>
/// <para>
/// <b>Anonymous on purpose, and read-only.</b> Every endpoint here answers a stranger, so each one
/// carries the per-address limit of <see cref="RateLimitingConfiguration.SiteShopPolicy"/>: without
/// it anyone could send requests as fast as the network allows, and each one reaches the database.
/// </para>
/// </remarks>
public static class SiteShopEndpoints
{
    public static IEndpointRouteBuilder MapSiteShopEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup($"{SitePaths.Root}/shop")
            .WithTags("Site shop")
            .RequireRateLimiting(RateLimitingConfiguration.SiteShopPolicy)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/showcase", async (GetShowcaseHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(ct)))
            .AllowAnonymous()
            .WithName("GetShowcase")
            .Produces<ShowcaseResponse>();

        group.MapGet("/products/{id:guid}", async (Guid id, GetShowcaseProductHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttpResult())
            .AllowAnonymous()
            .WithName("GetShowcaseProduct")
            .Produces<ShowcaseProductResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // The photos of shown products, at an address the public host can reach (the panel's
        // /api/shop/photos serves hidden products' too, for the shop admin). A photo's id never
        // gets other bytes, so a browser may keep it for a year, and a second visit asks for none.
        group.MapGet("/photos/{photoId:guid}/{size}", async (
                Guid photoId, string size, GetShowcasePhotoHandler handler, HttpResponse response, CancellationToken ct) =>
                (await handler.Handle(photoId, size, ct)).ToHttpResult(stream =>
                {
                    response.Headers[HeaderNames.CacheControl] = "public, max-age=31536000, immutable";
                    return Results.File(stream, "image/webp");
                }))
            .AllowAnonymous()
            .WithName("GetShowcasePhoto")
            .Produces(StatusCodes.Status200OK, contentType: "image/webp")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
