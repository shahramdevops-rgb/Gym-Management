using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Common.Paging;
using Gym.Application.History.ListAttendance;
using Gym.Application.History.ListPayments;
using Gym.Application.History.ListSales;
using Gym.Application.History.ListServiceCharges;

namespace Gym.Api.Endpoints;

/// <summary>
/// The gym's history (تاریخچه): every check-in, payment and هوازی, newest first, with who recorded
/// it (BUSINESS_RULES.md §12 <i>History</i>, roadmap 6.5.25). Both roles open all three lists;
/// how far back Staff may read payments is the handler's rule, because it depends on today and on
/// who is asking, not only on the role. The sales list (فروش‌ها, roadmap 6.5.30) is the Owner's
/// alone (§12 <i>Sales in the history</i>).
/// </summary>
/// <remarks>
/// Each list sits at the root of its own resource (<c>/api/attendance</c>, <c>/api/payments</c>,
/// <c>/api/service-charges</c>), next to the per-member and per-item routes the other endpoint
/// files already map there.
/// </remarks>
public static class HistoryEndpoints
{
    public static IEndpointRouteBuilder MapHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        Group(app, "/api/attendance").MapGet("/", async (
                [AsParameters] ListAttendanceQuery query,
                ListAttendanceHandler handler,
                CancellationToken ct) =>
                    Results.Ok(await handler.Handle(query, ct)))
            .AddEndpointFilter<ValidationFilter<ListAttendanceQuery>>()
            .WithName("ListAttendance")
            .Produces<PagedResponse<HistoryAttendanceResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        // 403 also when Staff reach further back than the last few days (Payments.HistoryTooFarBack).
        Group(app, "/api/payments").MapGet("/", async (
                [AsParameters] ListPaymentsQuery query,
                ListPaymentsHandler handler,
                CancellationToken ct) =>
                    (await handler.Handle(query, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<ListPaymentsQuery>>()
            .WithName("ListPayments")
            .Produces<PagedResponse<HistoryPaymentResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        Group(app, "/api/service-charges").MapGet("/", async (
                [AsParameters] ListServiceChargesQuery query,
                ListServiceChargesHandler handler,
                CancellationToken ct) =>
                    Results.Ok(await handler.Handle(query, ct)))
            .AddEndpointFilter<ValidationFilter<ListServiceChargesQuery>>()
            .WithName("ListServiceCharges")
            .Produces<PagedResponse<HistoryServiceChargeResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        Group(app, "/api/sales", Policies.OwnerOnly).MapGet("/", async (
                [AsParameters] ListSalesQuery query,
                ListSalesHandler handler,
                CancellationToken ct) =>
                    Results.Ok(await handler.Handle(query, ct)))
            .AddEndpointFilter<ValidationFilter<ListSalesQuery>>()
            .WithName("ListSales")
            .Produces<PagedResponse<HistorySaleResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static RouteGroupBuilder Group(
        IEndpointRouteBuilder app, string prefix, string policy = Policies.StaffOrOwner) =>
        app.MapGroup(prefix)
            .WithTags("History")
            .RequireAuthorization(policy)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
}
