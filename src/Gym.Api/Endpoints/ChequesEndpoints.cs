using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Cheques;
using Gym.Application.Cheques.CancelCheque;
using Gym.Application.Cheques.GetCheque;
using Gym.Application.Cheques.ListCheques;
using Gym.Application.Cheques.MarkChequePassed;
using Gym.Application.Cheques.RegisterCheque;
using Gym.Application.Cheques.UpdateCheque;

namespace Gym.Api.Endpoints;

/// <summary>
/// The cheques the gym gave (BUSINESS_RULES.md §9 <i>Cheques</i>). Owner only, reading included (§1).
/// </summary>
/// <remarks>
/// A cheque is edited while pending, then passed or cancelled with a reason, never deleted, so
/// there is no DELETE here.
/// </remarks>
public static class ChequesEndpoints
{
    private const string Prefix = "/api/cheques";

    public static IEndpointRouteBuilder MapChequesEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup(Prefix)
            .WithTags("Cheques")
            .RequireAuthorization(Policies.OwnerOnly)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", async (
                [AsParameters] ListChequesQuery query,
                ListChequesHandler handler,
                CancellationToken ct) =>
                    Results.Ok(await handler.Handle(query, ct)))
            .AddEndpointFilter<ValidationFilter<ListChequesQuery>>()
            .WithName("ListCheques")
            .Produces<ChequeListResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", async (
                Guid id,
                GetChequeHandler handler,
                CancellationToken ct) =>
                    (await handler.Handle(id, ct)).ToHttpResult())
            .WithName("GetCheque")
            .Produces<ChequeResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                RegisterChequeCommand command,
                RegisterChequeHandler handler,
                CancellationToken ct) =>
                    (await handler.Handle(command, ct))
                        .ToHttpResult(cheque => Results.Created($"{Prefix}/{cheque.Id}", cheque)))
            .AddEndpointFilter<ValidationFilter<RegisterChequeCommand>>()
            .WithName("RegisterCheque")
            .Produces<ChequeResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateChequeCommand command,
                UpdateChequeHandler handler,
                CancellationToken ct) =>
                    (await handler.Handle(id, command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<UpdateChequeCommand>>()
            .WithName("UpdateCheque")
            .Produces<ChequeResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/pass", async (
                Guid id,
                MarkChequePassedHandler handler,
                CancellationToken ct) =>
                    (await handler.Handle(id, ct)).ToHttpResult())
            .WithName("MarkChequePassed")
            .Produces<ChequeResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/cancel", async (
                Guid id,
                CancelChequeCommand command,
                CancelChequeHandler handler,
                CancellationToken ct) =>
                    (await handler.Handle(id, command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<CancelChequeCommand>>()
            .WithName("CancelCheque")
            .Produces<ChequeResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
