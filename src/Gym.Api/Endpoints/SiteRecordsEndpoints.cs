using System.Security.Claims;

using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Attendances.ListMemberAttendance;
using Gym.Application.Cafe.ListMemberCafeOrders;
using Gym.Application.Common.Paging;
using Gym.Application.Payments.ListMemberPayments;
using Gym.Application.SiteRecords;
using Gym.Application.SiteRecords.GetMyDebt;
using Gym.Application.SiteRecords.GetMyDetails;
using Gym.Application.SiteRecords.GetMyPlan;
using Gym.Application.SiteRecords.ListMyCafeOrders;
using Gym.Application.SiteRecords.ListMyPayments;
using Gym.Application.SiteRecords.ListMyPlans;
using Gym.Application.SiteRecords.ListMyVisits;
using Gym.Application.Subscriptions.ListMemberSubscriptions;

namespace Gym.Api.Endpoints;

/// <summary>
/// The member's own records on the site, read-only (BUSINESS_RULES.md §15 <i>The member's own
/// records</i>), behind <see cref="Policies.SiteMember"/>. No path names a member: each is the
/// signed-in account's own, so no one can ask for another member's. The handlers read the member
/// again from the database, since a token may be 15 minutes old: one the desk deactivated gets a
/// 403 at once.
/// </summary>
public static class SiteRecordsEndpoints
{
    public static IEndpointRouteBuilder MapSiteRecordsEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup($"{SitePaths.Root}/me")
            .WithTags("Site records")
            .RequireAuthorization(Policies.SiteMember)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/details", async (ClaimsPrincipal user, GetMyDetailsHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), ct)).ToHttpResult())
            .WithName("GetMyDetails")
            .Produces<MyDetailsResponse>();

        group.MapGet("/plan", async (ClaimsPrincipal user, GetMyPlanHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), ct)).ToHttpResult())
            .WithName("GetMyPlan")
            .Produces<MyPlanResponse>();

        group.MapGet("/debt", async (ClaimsPrincipal user, GetMyDebtHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), ct)).ToHttpResult())
            .WithName("GetMyDebt")
            .Produces<MyDebtResponse>();

        group.MapGet("/visits", async (ClaimsPrincipal user, [AsParameters] ListMemberAttendanceQuery query, ListMyVisitsHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), query, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<ListMemberAttendanceQuery>>()
            .WithName("ListMyVisits")
            .Produces<PagedResponse<MyVisitResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/plans", async (ClaimsPrincipal user, [AsParameters] ListMemberSubscriptionsQuery query, ListMyPlansHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), query, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<ListMemberSubscriptionsQuery>>()
            .WithName("ListMyPlans")
            .Produces<PagedResponse<SitePlanResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/payments", async (ClaimsPrincipal user, [AsParameters] ListMemberPaymentsQuery query, ListMyPaymentsHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), query, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<ListMemberPaymentsQuery>>()
            .WithName("ListMyPayments")
            .Produces<PagedResponse<MyPaymentResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/cafe-orders", async (ClaimsPrincipal user, [AsParameters] ListMemberCafeOrdersQuery query, ListMyCafeOrdersHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), query, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<ListMemberCafeOrdersQuery>>()
            .WithName("ListMyCafeOrders")
            .Produces<PagedResponse<MyCafeOrderResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }
}
