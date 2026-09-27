using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Expenses;
using Gym.Application.Expenses.GetExpense;
using Gym.Application.Expenses.ListExpenses;
using Gym.Application.Expenses.RecordExpense;
using Gym.Application.Expenses.UpdateExpense;
using Gym.Application.Expenses.VoidExpense;

namespace Gym.Api.Endpoints;

/// <summary>
/// Money the gym paid out (BUSINESS_RULES.md §9). Owner only, reading included (§1).
/// </summary>
/// <remarks>
/// An expense is edited while it stands and voided with a reason, never deleted, so there is no
/// DELETE here.
/// </remarks>
public static class ExpensesEndpoints
{
    private const string Prefix = "/api/expenses";

    public static IEndpointRouteBuilder MapExpensesEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup(Prefix)
            .WithTags("Expenses")
            .RequireAuthorization(Policies.OwnerOnly)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", async (
                [AsParameters] ListExpensesQuery query,
                ListExpensesHandler handler,
                CancellationToken ct) =>
                    Results.Ok(await handler.Handle(query, ct)))
            .AddEndpointFilter<ValidationFilter<ListExpensesQuery>>()
            .WithName("ListExpenses")
            .Produces<ExpenseListResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", async (
                Guid id,
                GetExpenseHandler handler,
                CancellationToken ct) =>
                    (await handler.Handle(id, ct)).ToHttpResult())
            .WithName("GetExpense")
            .Produces<ExpenseResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                RecordExpenseCommand command,
                RecordExpenseHandler handler,
                CancellationToken ct) =>
                    (await handler.Handle(command, ct))
                        .ToHttpResult(expense => Results.Created($"{Prefix}/{expense.Id}", expense)))
            .AddEndpointFilter<ValidationFilter<RecordExpenseCommand>>()
            .WithName("RecordExpense")
            .Produces<ExpenseResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateExpenseCommand command,
                UpdateExpenseHandler handler,
                CancellationToken ct) =>
                    (await handler.Handle(id, command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<UpdateExpenseCommand>>()
            .WithName("UpdateExpense")
            .Produces<ExpenseResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/void", async (
                Guid id,
                VoidExpenseCommand command,
                VoidExpenseHandler handler,
                CancellationToken ct) =>
                    (await handler.Handle(id, command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<VoidExpenseCommand>>()
            .WithName("VoidExpense")
            .Produces<ExpenseResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
