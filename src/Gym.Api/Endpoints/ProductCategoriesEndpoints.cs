using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Cafe;
using Gym.Application.Cafe.CreateProductCategory;
using Gym.Application.Cafe.DeleteProductCategory;
using Gym.Application.Cafe.ListProductCategories;
using Gym.Application.Cafe.SetProductCategoryActive;
using Gym.Application.Cafe.UpdateProductCategory;
using Gym.Application.Common.Paging;

namespace Gym.Api.Endpoints;

/// <summary>
/// The cafe's categories. Both roles, reading and writing (BUSINESS_RULES.md §1: "Cafe products
/// and categories"): the Owner decided staff have no restriction in the cafe at all, because the
/// front desk is where a new shelf appears and where it runs out.
/// </summary>
/// <remarks>
/// One group, one policy, because every endpoint here shares it. The policy is still named
/// explicitly rather than left to a default, so widening or narrowing it is a decision somebody
/// makes on purpose.
/// </remarks>
public static class ProductCategoriesEndpoints
{
    private const string Prefix = "/api/cafe/categories";

    public static IEndpointRouteBuilder MapProductCategoriesEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup(Prefix)
            .WithTags("Cafe")
            .RequireAuthorization(Policies.StaffOrOwner)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", async (
                [AsParameters] ListProductCategoriesQuery query,
                ListProductCategoriesHandler handler,
                CancellationToken ct) =>
                    Results.Ok(await handler.Handle(query, ct)))
            .AddEndpointFilter<ValidationFilter<ListProductCategoriesQuery>>()
            .WithName("ListProductCategories")
            .Produces<PagedResponse<ProductCategoryResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/", async (
                CreateProductCategoryCommand command,
                CreateProductCategoryHandler handler,
                CancellationToken ct) =>
                    (await handler.Handle(command, ct))
                        .ToHttpResult(category => Results.Created($"{Prefix}/{category.Id}", category)))
            .AddEndpointFilter<ValidationFilter<CreateProductCategoryCommand>>()
            .WithName("CreateProductCategory")
            .Produces<ProductCategoryResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateProductCategoryCommand command,
                UpdateProductCategoryHandler handler,
                CancellationToken ct) =>
                    (await handler.Handle(id, command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<UpdateProductCategoryCommand>>()
            .WithName("UpdateProductCategory")
            .Produces<ProductCategoryResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
                Guid id,
                DeleteProductCategoryHandler handler,
                CancellationToken ct) =>
                    (await handler.Handle(id, ct)).ToHttpResult())
            .WithName("DeleteProductCategory")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/activate", async (
                Guid id,
                SetProductCategoryActiveHandler handler,
                CancellationToken ct) =>
                    (await handler.Activate(id, ct)).ToHttpResult())
            .WithName("ActivateProductCategory")
            .Produces<ProductCategoryResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/deactivate", async (
                Guid id,
                SetProductCategoryActiveHandler handler,
                CancellationToken ct) =>
                    (await handler.Deactivate(id, ct)).ToHttpResult())
            .WithName("DeactivateProductCategory")
            .Produces<ProductCategoryResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
