using Gym.Api.Authorization;
using Gym.Api.Filters;
using Gym.Application.Common.Paging;
using Gym.Application.GuestDebts.ListGuestDebts;

namespace Gym.Api.Endpoints;

/// <summary>
/// «بدهی مهمان‌ها»: what guests' visits still owe, cafe orders and service charges together
/// (BUSINESS_RULES.md §7 <i>Guest visit</i>, roadmap 6.5.31). Front-desk work, so both roles. Paying
/// or voiding a row goes through that item's own endpoints, under <c>/api/cafe-orders</c> and
/// <c>/api/service-charges</c>; this list only reads.
/// </summary>
public static class GuestDebtsEndpoints
{
    public static IEndpointRouteBuilder MapGuestDebtsEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGroup("/api/guest-debts")
            .WithTags("GuestDebts")
            .RequireAuthorization(Policies.StaffOrOwner)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .MapGet("/", async (
                [AsParameters] ListGuestDebtsQuery query,
                ListGuestDebtsHandler handler,
                CancellationToken ct) =>
                    Results.Ok(await handler.Handle(query, ct)))
            .AddEndpointFilter<ValidationFilter<ListGuestDebtsQuery>>()
            .WithName("ListGuestDebts")
            .Produces<PagedResponse<GuestDebtResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }
}
