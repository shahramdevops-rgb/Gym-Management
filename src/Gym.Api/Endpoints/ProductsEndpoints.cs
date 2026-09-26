using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Cafe;
using Gym.Application.Cafe.CreateProduct;
using Gym.Application.Cafe.GetProduct;
using Gym.Application.Cafe.ListProducts;
using Gym.Application.Cafe.SetProductActive;
using Gym.Application.Cafe.UpdateProduct;
using Gym.Application.Common.Paging;

namespace Gym.Api.Endpoints;

/// <summary>
/// The cafe's price list. Both roles, reading and writing (BUSINESS_RULES.md §1: "Cafe products",
/// changed by the Owner when Phase 7 started): the person who sees a new box arrive, with its
/// price on it, is the one at the desk, and making them wait for the Owner means the item is sold
/// off the books.
/// </summary>
/// <remarks>
/// One group, one policy, because every endpoint here shares it. The categories next door are
/// Owner-only, which is why they are a separate file rather than a second group with a default
/// that could quietly widen.
/// </remarks>
public static class ProductsEndpoints
{
    private const string Prefix = "/api/cafe/products";

    public static IEndpointRouteBuilder MapProductsEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup(Prefix)
            .WithTags("Cafe")
            .RequireAuthorization(Policies.StaffOrOwner)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", async (
                [AsParameters] ListProductsQuery query,
                ListProductsHandler handler,
                CancellationToken ct) =>
                    Results.Ok(await handler.Handle(query, ct)))
            .AddEndpointFilter<ValidationFilter<ListProductsQuery>>()
            .WithName("ListProducts")
            .Produces<PagedResponse<ProductResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", async (Guid id, GetProductHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttpResult())
            .WithName("GetProduct")
            .Produces<ProductResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                CreateProductCommand command,
                CreateProductHandler handler,
                CancellationToken ct) =>
                    (await handler.Handle(command, ct))
                        .ToHttpResult(product => Results.Created($"{Prefix}/{product.Id}", product)))
            .AddEndpointFilter<ValidationFilter<CreateProductCommand>>()
            .WithName("CreateProduct")
            .Produces<ProductResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateProductCommand command,
                UpdateProductHandler handler,
                CancellationToken ct) =>
                    (await handler.Handle(id, command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<UpdateProductCommand>>()
            .WithName("UpdateProduct")
            .Produces<ProductResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/activate", async (
                Guid id,
                SetProductActiveHandler handler,
                CancellationToken ct) =>
                    (await handler.Activate(id, ct)).ToHttpResult())
            .WithName("ActivateProduct")
            .Produces<ProductResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/deactivate", async (
                Guid id,
                SetProductActiveHandler handler,
                CancellationToken ct) =>
                    (await handler.Deactivate(id, ct)).ToHttpResult())
            .WithName("DeactivateProduct")
            .Produces<ProductResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
