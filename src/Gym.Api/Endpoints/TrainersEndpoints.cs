using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Trainers;
using Gym.Application.Trainers.CreateTrainer;
using Gym.Application.Trainers.ListTrainers;
using Gym.Application.Trainers.RestartTrainer;
using Gym.Application.Trainers.StopTrainer;
using Gym.Application.Trainers.UpdateTrainer;

namespace Gym.Api.Endpoints;

/// <summary>
/// «مربیان» in the panel (BUSINESS_RULES.md §15 <i>Trainers</i>). Owner only, reading included: only
/// the Owner makes someone a trainer.
/// </summary>
/// <remarks>
/// There is no delete: a trainer is stopped, and made one again with a new period.
/// </remarks>
public static class TrainersEndpoints
{
    private const string Prefix = "/api/trainers";

    public static IEndpointRouteBuilder MapTrainersEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup(Prefix)
            .WithTags("Trainers")
            .RequireAuthorization(Policies.OwnerOnly)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", async (ListTrainersHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(ct)))
            .WithName("ListTrainers")
            .Produces<IReadOnlyList<TrainerResponse>>();

        group.MapPost("/", async (CreateTrainerCommand command, CreateTrainerHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct))
                    .ToHttpResult(trainer => Results.Created($"{Prefix}/{trainer.Id}", trainer)))
            .AddEndpointFilter<ValidationFilter<CreateTrainerCommand>>()
            .WithName("CreateTrainer")
            .Produces<TrainerResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (Guid id, UpdateTrainerCommand command, UpdateTrainerHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<UpdateTrainerCommand>>()
            .WithName("UpdateTrainer")
            .Produces<TrainerResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/stop", async (Guid id, StopTrainerHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttpResult())
            .WithName("StopTrainer")
            .Produces<TrainerResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/restart", async (Guid id, RestartTrainerHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttpResult())
            .WithName("RestartTrainer")
            .Produces<TrainerResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
