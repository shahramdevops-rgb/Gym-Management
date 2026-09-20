using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Common.Paging;
using Gym.Application.Lockers;
using Gym.Application.Lockers.CreateLocker;
using Gym.Application.Lockers.GetLocker;
using Gym.Application.Lockers.ListLockers;
using Gym.Application.Lockers.SetLockerOutOfService;

namespace Gym.Api.Endpoints;

/// <summary>
/// Lockers. Setting them up and reading them back is the Owner's job (BUSINESS_RULES.md §1,
/// permissions: "lockers setup"): check-in (task 5.2) picks a free locker itself, so staff never
/// need to browse the list.
/// </summary>
public static class LockersEndpoints
{
    private const string Prefix = "/api/lockers";

    public static IEndpointRouteBuilder MapLockersEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup(Prefix)
            .WithTags("Lockers")
            .RequireAuthorization(Policies.OwnerOnly)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/", async (CreateLockerCommand command, CreateLockerHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttpResult(locker => Results.Created($"{Prefix}/{locker.Id}", locker)))
            .AddEndpointFilter<ValidationFilter<CreateLockerCommand>>()
            .WithName("CreateLocker")
            .Produces<LockerResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", async ([AsParameters] ListLockersQuery query, ListLockersHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(query, ct)))
            .AddEndpointFilter<ValidationFilter<ListLockersQuery>>()
            .WithName("ListLockers")
            .Produces<PagedResponse<LockerResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", async (Guid id, GetLockerHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttpResult())
            .WithName("GetLocker")
            .Produces<LockerResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/out-of-service", async (Guid id, SetLockerOutOfServiceHandler handler, CancellationToken ct) =>
                (await handler.MarkOutOfService(id, ct)).ToHttpResult())
            .WithName("SetLockerOutOfService")
            .Produces<LockerResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/in-service", async (Guid id, SetLockerOutOfServiceHandler handler, CancellationToken ct) =>
                (await handler.MarkInService(id, ct)).ToHttpResult())
            .WithName("SetLockerInService")
            .Produces<LockerResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
