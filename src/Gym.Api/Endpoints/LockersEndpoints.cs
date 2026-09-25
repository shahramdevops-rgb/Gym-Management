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
/// Lockers. Adding one is the Owner's job, but reading the list and changing a locker's service
/// state are the front desk's (BUSINESS_RULES.md §1 permissions, §6): the person who finds a
/// locker broken is the one standing at it.
/// </summary>
/// <remarks>
/// The policy is named on every endpoint rather than once on the group. A group default is how
/// this ended up Owner-only in the first place — an endpoint added later inherits an access rule
/// nobody chose for it.
/// </remarks>
public static class LockersEndpoints
{
    private const string Prefix = "/api/lockers";

    public static IEndpointRouteBuilder MapLockersEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup(Prefix)
            .WithTags("Lockers")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/", async (CreateLockerCommand command, CreateLockerHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttpResult(locker => Results.Created($"{Prefix}/{locker.Id}", locker)))
            .AddEndpointFilter<ValidationFilter<CreateLockerCommand>>()
            .RequireAuthorization(Policies.OwnerOnly)
            .WithName("CreateLocker")
            .Produces<LockerResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", async ([AsParameters] ListLockersQuery query, ListLockersHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(query, ct)))
            .AddEndpointFilter<ValidationFilter<ListLockersQuery>>()
            .RequireAuthorization(Policies.StaffOrOwner)
            .WithName("ListLockers")
            .Produces<PagedResponse<LockerResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", async (Guid id, GetLockerHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttpResult())
            .RequireAuthorization(Policies.StaffOrOwner)
            .WithName("GetLocker")
            .Produces<LockerResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/out-of-service", async (Guid id, SetLockerOutOfServiceHandler handler, CancellationToken ct) =>
                (await handler.MarkOutOfService(id, ct)).ToHttpResult())
            .RequireAuthorization(Policies.StaffOrOwner)
            .WithName("SetLockerOutOfService")
            .Produces<LockerResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/in-service", async (Guid id, SetLockerOutOfServiceHandler handler, CancellationToken ct) =>
                (await handler.MarkInService(id, ct)).ToHttpResult())
            .RequireAuthorization(Policies.StaffOrOwner)
            .WithName("SetLockerInService")
            .Produces<LockerResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
