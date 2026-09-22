using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Common.Paging;
using Gym.Application.Members;
using Gym.Application.Members.CreateMember;
using Gym.Application.Members.GetMember;
using Gym.Application.Members.GetMemberDebt;
using Gym.Application.Members.ListMembers;
using Gym.Application.Members.SetMemberActive;
using Gym.Application.Members.UpdateMember;

namespace Gym.Api.Endpoints;

/// <summary>
/// Members. Front-desk work, so both roles (BUSINESS_RULES.md §1, permissions: "Members:
/// create, update, deactivate, search").
/// </summary>
public static class MembersEndpoints
{
    public static IEndpointRouteBuilder MapMembersEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/members")
            .WithTags("Members")
            .RequireAuthorization(Policies.StaffOrOwner)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", async ([AsParameters] ListMembersQuery query, ListMembersHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(query, ct)))
            .AddEndpointFilter<ValidationFilter<ListMembersQuery>>()
            .WithName("ListMembers")
            .Produces<PagedResponse<MemberResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", async (Guid id, GetMemberHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttpResult())
            .WithName("GetMember")
            .Produces<MemberResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // What the member still owes, item by item (BUSINESS_RULES.md §5 Member debt, task 4.7).
        group.MapGet("/{id:guid}/debt", async (Guid id, GetMemberDebtHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttpResult())
            .WithName("GetMemberDebt")
            .Produces<MemberDebtResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (CreateMemberCommand command, CreateMemberHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttpResult(member => Results.Created($"/api/members/{member.Id}", member)))
            .AddEndpointFilter<ValidationFilter<CreateMemberCommand>>()
            .WithName("CreateMember")
            .Produces<MemberResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (Guid id, UpdateMemberCommand command, UpdateMemberHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<UpdateMemberCommand>>()
            .WithName("UpdateMember")
            .Produces<MemberResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/deactivate", async (Guid id, SetMemberActiveHandler handler, CancellationToken ct) =>
                (await handler.Deactivate(id, ct)).ToHttpResult())
            .WithName("DeactivateMember")
            .Produces<MemberResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/reactivate", async (Guid id, SetMemberActiveHandler handler, CancellationToken ct) =>
                (await handler.Reactivate(id, ct)).ToHttpResult())
            .WithName("ReactivateMember")
            .Produces<MemberResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
