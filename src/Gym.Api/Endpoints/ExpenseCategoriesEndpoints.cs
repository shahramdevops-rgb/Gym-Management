using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Common.Paging;
using Gym.Application.Expenses;
using Gym.Application.Expenses.CreateExpenseCategory;
using Gym.Application.Expenses.ListExpenseCategories;
using Gym.Application.Expenses.UpdateExpenseCategory;

namespace Gym.Api.Endpoints;

/// <summary>
/// The headings expenses are filed under. Owner only, reading included (BUSINESS_RULES.md §1:
/// "Expenses"): what the gym spends is the Owner's business, not the front desk's.
/// </summary>
/// <remarks>
/// There is no delete and no switch: a category is only ever added or renamed (§9), because
/// expenses and reports point at it.
/// </remarks>
public static class ExpenseCategoriesEndpoints
{
    private const string Prefix = "/api/expenses/categories";

    public static IEndpointRouteBuilder MapExpenseCategoriesEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup(Prefix)
            .WithTags("Expenses")
            .RequireAuthorization(Policies.OwnerOnly)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", async (
                [AsParameters] ListExpenseCategoriesQuery query,
                ListExpenseCategoriesHandler handler,
                CancellationToken ct) =>
                    Results.Ok(await handler.Handle(query, ct)))
            .AddEndpointFilter<ValidationFilter<ListExpenseCategoriesQuery>>()
            .WithName("ListExpenseCategories")
            .Produces<PagedResponse<ExpenseCategoryResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/", async (
                CreateExpenseCategoryCommand command,
                CreateExpenseCategoryHandler handler,
                CancellationToken ct) =>
                    (await handler.Handle(command, ct))
                        .ToHttpResult(category => Results.Created($"{Prefix}/{category.Id}", category)))
            .AddEndpointFilter<ValidationFilter<CreateExpenseCategoryCommand>>()
            .WithName("CreateExpenseCategory")
            .Produces<ExpenseCategoryResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateExpenseCategoryCommand command,
                UpdateExpenseCategoryHandler handler,
                CancellationToken ct) =>
                    (await handler.Handle(id, command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<UpdateExpenseCategoryCommand>>()
            .WithName("UpdateExpenseCategory")
            .Produces<ExpenseCategoryResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
