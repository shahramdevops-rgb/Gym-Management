using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Members;
using Gym.Application.Members.CreateMember;
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

        return app;
    }
}
