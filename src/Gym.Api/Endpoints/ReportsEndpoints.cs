using Gym.Api.Authorization;
using Gym.Api.Filters;
using Gym.Application.Reports.GetAttendanceReport;
using Gym.Application.Reports.GetFinancialReport;
using Gym.Application.Reports.GetMembersReport;
using Gym.Application.Reports.GetNeedsAttention;
using Gym.Application.Reports.GetReceivables;
using Gym.Application.Reports.GetSubscriptionsSnapshot;
using Gym.Application.Reports.GetTopCafeProducts;

namespace Gym.Api.Endpoints;

/// <summary>
/// The figures behind the Owner's dashboard (BUSINESS_RULES.md §12 <i>Financial report</i>,
/// <i>Receivables</i>, <i>Operational reports</i>, <i>Needs attention</i>, roadmap 9.1 and 9.2).
/// Owner only, reading included.
/// </summary>
public static class ReportsEndpoints
{
    public static IEndpointRouteBuilder MapReportsEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var reports = app.MapGroup("/api/reports")
            .WithTags("Reports")
            .RequireAuthorization(Policies.OwnerOnly)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        reports.MapGet("/financial", async (
                [AsParameters] GetFinancialReportQuery query,
                GetFinancialReportHandler handler,
                CancellationToken ct) =>
                    Results.Ok(await handler.Handle(query, ct)))
            .AddEndpointFilter<ValidationFilter<GetFinancialReportQuery>>()
            .WithName("GetFinancialReport")
            .Produces<FinancialReportResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        reports.MapGet("/receivables", async (
                GetReceivablesHandler handler,
                CancellationToken ct) =>
                    Results.Ok(await handler.Handle(ct)))
            .WithName("GetReceivables")
            .Produces<ReceivablesResponse>();

        reports.MapGet("/attendance", async (
                [AsParameters] GetAttendanceReportQuery query,
                GetAttendanceReportHandler handler,
                CancellationToken ct) =>
                    Results.Ok(await handler.Handle(query, ct)))
            .AddEndpointFilter<ValidationFilter<GetAttendanceReportQuery>>()
            .WithName("GetAttendanceReport")
            .Produces<AttendanceReportResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        reports.MapGet("/subscriptions", async (
                GetSubscriptionsSnapshotHandler handler,
                CancellationToken ct) =>
                    Results.Ok(await handler.Handle(ct)))
            .WithName("GetSubscriptionsSnapshot")
            .Produces<SubscriptionsSnapshotResponse>();

        reports.MapGet("/cafe-products", async (
                [AsParameters] GetTopCafeProductsQuery query,
                GetTopCafeProductsHandler handler,
                CancellationToken ct) =>
                    Results.Ok(await handler.Handle(query, ct)))
            .AddEndpointFilter<ValidationFilter<GetTopCafeProductsQuery>>()
            .WithName("GetTopCafeProducts")
            .Produces<IReadOnlyList<TopCafeProductResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        reports.MapGet("/needs-attention", async (
                GetNeedsAttentionHandler handler,
                CancellationToken ct) =>
                    Results.Ok(await handler.Handle(ct)))
            .WithName("GetNeedsAttention")
            .Produces<NeedsAttentionResponse>();

        reports.MapGet("/members", async (
                [AsParameters] GetMembersReportQuery query,
                GetMembersReportHandler handler,
                CancellationToken ct) =>
                    Results.Ok(await handler.Handle(query, ct)))
            .AddEndpointFilter<ValidationFilter<GetMembersReportQuery>>()
            .WithName("GetMembersReport")
            .Produces<MembersReportResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }
}
