using Gym.Api.Authorization;
using Gym.Api.Filters;
using Gym.Application.Reports.GetFinancialReport;
using Gym.Application.Reports.GetReceivables;

namespace Gym.Api.Endpoints;

/// <summary>
/// The figures behind the Owner's dashboard (BUSINESS_RULES.md §12 <i>Financial report</i>,
/// <i>Receivables</i>, roadmap 9.1). Owner only, reading included.
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

        return app;
    }
}
