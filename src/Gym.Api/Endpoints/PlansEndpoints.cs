using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Common.Paging;
using Gym.Application.Plans;
using Gym.Application.Plans.CreatePlan;
using Gym.Application.Plans.GetPlan;
using Gym.Application.Plans.ListPlans;
using Gym.Application.Plans.SetPlanActive;
using Gym.Application.Plans.UpdatePlan;

namespace Gym.Api.Endpoints;

/// <summary>
/// Plans. Setting them up is the Owner's job (BUSINESS_RULES.md §1, permissions: "Plans ...
/// setup"), but staff sell subscriptions, so both roles can read them (decided in task 3.1).
/// </summary>
/// <remarks>
/// Two groups on one prefix, one per policy, so each endpoint gets its policy from the group it
/// is mapped in and a new write endpoint cannot be added without the Owner check.
/// </remarks>
public static class PlansEndpoints
{
    private const string Prefix = "/api/plans";

    public static IEndpointRouteBuilder MapPlansEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var read = app.MapGroup(Prefix)
            .WithTags("Plans")
            .RequireAuthorization(Policies.StaffOrOwner)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        read.MapGet("/", async ([AsParameters] ListPlansQuery query, ListPlansHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(query, ct)))
            .AddEndpointFilter<ValidationFilter<ListPlansQuery>>()
            .WithName("ListPlans")
            .Produces<PagedResponse<PlanResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        read.MapGet("/{id:guid}", async (Guid id, GetPlanHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttpResult())
            .WithName("GetPlan")
            .Produces<PlanResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        var write = app.MapGroup(Prefix)
            .WithTags("Plans")
            .RequireAuthorization(Policies.OwnerOnly)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        write.MapPost("/", async (CreatePlanCommand command, CreatePlanHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttpResult(plan => Results.Created($"{Prefix}/{plan.Id}", plan)))
            .AddEndpointFilter<ValidationFilter<CreatePlanCommand>>()
            .WithName("CreatePlan")
            .Produces<PlanResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        write.MapPut("/{id:guid}", async (Guid id, UpdatePlanCommand command, UpdatePlanHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<UpdatePlanCommand>>()
            .WithName("UpdatePlan")
            .Produces<PlanResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        write.MapPost("/{id:guid}/activate", async (Guid id, SetPlanActiveHandler handler, CancellationToken ct) =>
                (await handler.Activate(id, ct)).ToHttpResult())
            .WithName("ActivatePlan")
            .Produces<PlanResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        write.MapPost("/{id:guid}/deactivate", async (Guid id, SetPlanActiveHandler handler, CancellationToken ct) =>
                (await handler.Deactivate(id, ct)).ToHttpResult())
            .WithName("DeactivatePlan")
            .Produces<PlanResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
