using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Common.Paging;
using Gym.Application.Members;
using Gym.Application.SiteUsers;
using Gym.Application.SiteUsers.FindSiteUserByPhone;
using Gym.Application.SiteUsers.ListSiteUsers;
using Gym.Application.SiteUsers.RegisterSiteUser;

namespace Gym.Api.Endpoints;

/// <summary>
/// Site users (BUSINESS_RULES.md §16 <i>Site users</i>, task 16.1): people who signed up on the site
/// and never came. The Owner and Staff see them, as the member list (the developer, 1405/08/19).
/// </summary>
public static class SiteUsersEndpoints
{
    public static IEndpointRouteBuilder MapSiteUsersEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/site-users")
            .WithTags("SiteUsers")
            .RequireAuthorization(Policies.StaffOrOwner)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // The «کاربران سایت» page, read-only.
        group.MapGet("/", async ([AsParameters] ListSiteUsersQuery query, ListSiteUsersHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(query, ct)))
            .AddEndpointFilter<ValidationFilter<ListSiteUsersQuery>>()
            .WithName("ListSiteUsers")
            .Produces<PagedResponse<SiteUserResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        // The new-member form's look at a whole phone number.
        group.MapGet("/by-phone", async (string? phoneNumber, FindSiteUserByPhoneHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(phoneNumber, ct)))
            .WithName("FindSiteUserByPhone")
            .Produces<SiteUserByPhoneResponse>();

        // «ثبت به‌عنوان عضو»: from then on a member like any other.
        group.MapPost("/{id:guid}/register", async (Guid id, RegisterSiteUserHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttpResult())
            .WithName("RegisterSiteUser")
            .Produces<MemberResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
