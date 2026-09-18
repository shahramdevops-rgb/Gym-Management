using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Common.Paging;
using Gym.Application.Staff;
using Gym.Application.Staff.CreateStaff;
using Gym.Application.Staff.GetStaff;
using Gym.Application.Staff.ListStaff;
using Gym.Application.Staff.ResetStaffPassword;
using Gym.Application.Staff.SetStaffActive;

namespace Gym.Api.Endpoints;

/// <summary>
/// Staff account management. Owner only (BUSINESS_RULES.md §1, permissions table: "staff accounts").
/// </summary>
public static class StaffEndpoints
{
    public static IEndpointRouteBuilder MapStaffEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // On the group, so every endpoint below inherits it and none can be added without it.
        var group = app.MapGroup("/api/staff")
            .WithTags("Staff")
            .RequireAuthorization(Policies.OwnerOnly)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", async ([AsParameters] ListStaffQuery query, ListStaffHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(query, ct)))
            .AddEndpointFilter<ValidationFilter<ListStaffQuery>>()
            .WithName("ListStaff")
            .Produces<PagedResponse<StaffResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", async (Guid id, GetStaffHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttpResult())
            .WithName("GetStaff")
            .Produces<StaffResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (CreateStaffCommand command, CreateStaffHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttpResult(staff => Results.Created($"/api/staff/{staff.Id}", staff)))
            .AddEndpointFilter<ValidationFilter<CreateStaffCommand>>()
            .WithName("CreateStaff")
            .Produces<StaffResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/deactivate", async (Guid id, SetStaffActiveHandler handler, CancellationToken ct) =>
                (await handler.Deactivate(id, ct)).ToHttpResult())
            .WithName("DeactivateStaff")
            .Produces<StaffResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/reactivate", async (Guid id, SetStaffActiveHandler handler, CancellationToken ct) =>
                (await handler.Reactivate(id, ct)).ToHttpResult())
            .WithName("ReactivateStaff")
            .Produces<StaffResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/reset-password", async (Guid id, ResetStaffPasswordCommand command, ResetStaffPasswordHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<ResetStaffPasswordCommand>>()
            .WithName("ResetStaffPassword")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
