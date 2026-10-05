using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Payables;
using Gym.Application.Payables.CancelPayable;
using Gym.Application.Payables.GetPayable;
using Gym.Application.Payables.ListPayables;
using Gym.Application.Payables.ListPayablesDueSoon;
using Gym.Application.Payables.MarkPayablePaid;
using Gym.Application.Payables.RegisterPayable;
using Gym.Application.Payables.RevertPayable;
using Gym.Application.Payables.UpdatePayable;

namespace Gym.Api.Endpoints;

/// <summary>
/// The cheques the gym gave and its instalments, «چک و قسط» (BUSINESS_RULES.md §9 <i>Cheques and
/// instalments</i>). Owner only, reading included (§1).
/// </summary>
/// <remarks>
/// Edited while pending, paid (which records the expense), sent back to pending, or cancelled with a
/// reason; never deleted, so there is no DELETE here.
/// </remarks>
public static class PayablesEndpoints
{
    private const string Prefix = "/api/payables";

    public static IEndpointRouteBuilder MapPayablesEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup(Prefix)
            .WithTags("Payables")
            .RequireAuthorization(Policies.OwnerOnly)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", async (
                [AsParameters] ListPayablesQuery query,
                ListPayablesHandler handler,
                CancellationToken ct) =>
                    Results.Ok(await handler.Handle(query, ct)))
            .AddEndpointFilter<ValidationFilter<ListPayablesQuery>>()
            .WithName("ListPayables")
            .Produces<PayableListResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/due-soon", async (
                ListPayablesDueSoonHandler handler,
                CancellationToken ct) =>
                    Results.Ok(await handler.Handle(ct)))
            .WithName("ListPayablesDueSoon")
            .Produces<PayablesDueSoonResponse>();

        group.MapGet("/{id:guid}", async (
                Guid id,
                GetPayableHandler handler,
                CancellationToken ct) =>
                    (await handler.Handle(id, ct)).ToHttpResult())
            .WithName("GetPayable")
            .Produces<PayableResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                RegisterPayableCommand command,
                RegisterPayableHandler handler,
                CancellationToken ct) =>
                    (await handler.Handle(command, ct))
                        .ToHttpResult(payable => Results.Created($"{Prefix}/{payable.Id}", payable)))
            .AddEndpointFilter<ValidationFilter<RegisterPayableCommand>>()
            .WithName("RegisterPayable")
            .Produces<PayableResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdatePayableCommand command,
                UpdatePayableHandler handler,
                CancellationToken ct) =>
                    (await handler.Handle(id, command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<UpdatePayableCommand>>()
            .WithName("UpdatePayable")
            .Produces<PayableResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/pay", async (
                Guid id,
                MarkPayablePaidHandler handler,
                CancellationToken ct) =>
                    (await handler.Handle(id, ct)).ToHttpResult())
            .WithName("MarkPayablePaid")
            .Produces<PayableResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/revert", async (
                Guid id,
                RevertPayableCommand command,
                RevertPayableHandler handler,
                CancellationToken ct) =>
                    (await handler.Handle(id, command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<RevertPayableCommand>>()
            .WithName("RevertPayable")
            .Produces<PayableResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/cancel", async (
                Guid id,
                CancelPayableCommand command,
                CancelPayableHandler handler,
                CancellationToken ct) =>
                    (await handler.Handle(id, command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<CancelPayableCommand>>()
            .WithName("CancelPayable")
            .Produces<PayableResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
