using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Common.Files;
using Gym.Application.Shop;
using Gym.Application.Shop.AddShopPhoto;
using Gym.Application.Shop.CreateShopCategory;
using Gym.Application.Shop.CreateShopProduct;
using Gym.Application.Shop.DeleteShopCategory;
using Gym.Application.Shop.GetShopContacts;
using Gym.Application.Shop.GetShopPhoto;
using Gym.Application.Shop.GetShopProduct;
using Gym.Application.Shop.ListShopCategories;
using Gym.Application.Shop.ListShopProducts;
using Gym.Application.Shop.RemoveShopPhoto;
using Gym.Application.Shop.RenameShopCategory;
using Gym.Application.Shop.ReorderShopPhotos;
using Gym.Application.Shop.UpdateShopContacts;
using Gym.Application.Shop.UpdateShopProduct;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Gym.Api.Endpoints;

/// <summary>
/// The online shop's panel (ADR 0011, BUSINESS_RULES.md §16). The shop admin changes it
/// (<see cref="Policies.ShopAdmin"/>); the Owner reads it (<see cref="Policies.ShopOrOwner"/>); Staff
/// never reach it. A product photo is public (ADR 0011), the one anonymous endpoint here.
/// </summary>
public static class ShopEndpoints
{
    /// <summary>A little above the 10 MB a photo may be, for the form's own bytes.</summary>
    private const long PhotoRequestLimit = IImageProcessor.MaxUploadBytes + (512 * 1024);

    public static IEndpointRouteBuilder MapShopEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // No policy on the group: each endpoint names its own, reading or changing, so neither can
        // be added without deciding which.
        var shop = app.MapGroup("/api/shop")
            .WithTags("Shop")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        MapContacts(shop);
        MapCategories(shop);
        MapProducts(shop);
        MapPhotos(shop);

        return app;
    }

    private static void MapContacts(RouteGroupBuilder shop)
    {
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
    }

    private static void MapCategories(RouteGroupBuilder shop)
    {
        shop.MapGet("/categories", async (ListShopCategoriesHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(ct)))
            .RequireAuthorization(Policies.ShopOrOwner)
            .WithName("ListShopCategories")
            .Produces<IReadOnlyList<ShopCategoryResponse>>();

        shop.MapPost("/categories", async (CreateShopCategoryCommand command, CreateShopCategoryHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct))
                    .ToHttpResult(category => Results.Created($"/api/shop/categories/{category.Id}", category)))
            .AddEndpointFilter<ValidationFilter<CreateShopCategoryCommand>>()
            .RequireAuthorization(Policies.ShopAdmin)
            .WithName("CreateShopCategory")
            .Produces<ShopCategoryResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        shop.MapPut("/categories/{id:guid}", async (
                Guid id, RenameShopCategoryCommand command, RenameShopCategoryHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<RenameShopCategoryCommand>>()
            .RequireAuthorization(Policies.ShopAdmin)
            .WithName("RenameShopCategory")
            .Produces<ShopCategoryResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        shop.MapDelete("/categories/{id:guid}", async (Guid id, DeleteShopCategoryHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttpResult())
            .RequireAuthorization(Policies.ShopAdmin)
            .WithName("DeleteShopCategory")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static void MapProducts(RouteGroupBuilder shop)
    {
        shop.MapGet("/products", async (ListShopProductsHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(ct)))
            .RequireAuthorization(Policies.ShopOrOwner)
            .WithName("ListShopProducts")
            .Produces<IReadOnlyList<ShopProductListItem>>();

        shop.MapGet("/products/{id:guid}", async (Guid id, GetShopProductHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttpResult())
            .RequireAuthorization(Policies.ShopOrOwner)
            .WithName("GetShopProduct")
            .Produces<ShopProductResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        shop.MapPost("/products", async (CreateShopProductCommand command, CreateShopProductHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct))
                    .ToHttpResult(product => Results.Created($"/api/shop/products/{product.Id}", product)))
            .AddEndpointFilter<ValidationFilter<CreateShopProductCommand>>()
            .RequireAuthorization(Policies.ShopAdmin)
            .WithName("CreateShopProduct")
            .Produces<ShopProductResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        shop.MapPut("/products/{id:guid}", async (
                Guid id, UpdateShopProductCommand command, UpdateShopProductHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<UpdateShopProductCommand>>()
            .RequireAuthorization(Policies.ShopAdmin)
            .WithName("UpdateShopProduct")
            .Produces<ShopProductResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static void MapPhotos(RouteGroupBuilder shop)
    {
        // A multipart upload of one file named "file". The access token travels in the
        // Authorization header, never in a cookie, so a form posted from another site carries no
        // credentials and the antiforgery check has nothing to protect.
        shop.MapPost("/products/{id:guid}/photos", async (
                Guid id, IFormFile? file, AddShopPhotoHandler handler, CancellationToken ct) =>
                {
                    await using var content = file?.OpenReadStream();
                    return (await handler.Handle(id, content, file?.Length ?? 0, ct)).ToHttpResult();
                })
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(PhotoRequestLimit))
            .RequireAuthorization(Policies.ShopAdmin)
            .WithName("AddShopPhoto")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<ShopProductResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        shop.MapDelete("/products/{id:guid}/photos/{photoId:guid}", async (
                Guid id, Guid photoId, RemoveShopPhotoHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, photoId, ct)).ToHttpResult())
            .RequireAuthorization(Policies.ShopAdmin)
            .WithName("RemoveShopPhoto")
            .Produces<ShopProductResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        shop.MapPut("/products/{id:guid}/photos/order", async (
                Guid id, ReorderShopPhotosCommand command, ReorderShopPhotosHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, command, ct)).ToHttpResult())
            .RequireAuthorization(Policies.ShopAdmin)
            .WithName("ReorderShopPhotos")
            .Produces<ShopProductResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // Public: anyone may view a product photo (ADR 0011), the site's catalogue included. A
        // photo's id never gets other bytes, so a browser may keep it for a year.
        shop.MapGet("/photos/{photoId:guid}/{size}", async (
                Guid photoId, string size, GetShopPhotoHandler handler, HttpResponse response, CancellationToken ct) =>
                (await handler.Handle(photoId, size, ct)).ToHttpResult(stream =>
                {
                    response.Headers[HeaderNames.CacheControl] = "public, max-age=31536000, immutable";
                    return Results.File(stream, "image/webp");
                }))
            .AllowAnonymous()
            .WithName("GetShopPhoto")
            .Produces(StatusCodes.Status200OK, contentType: "image/webp")
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
