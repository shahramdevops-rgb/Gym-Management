using Gym.Api.Authorization;
using Gym.Api.Filters;
using Gym.Application.Audit.ListAuditLogs;
using Gym.Application.Audit.ListAuditUsers;
using Gym.Application.Common;
using Gym.Application.Common.Paging;

namespace Gym.Api.Endpoints;

/// <summary>
/// The audit screen, «گزارش تغییرات» (BUSINESS_RULES.md §11 <i>The audit screen</i>). Owner only
/// (§1, permissions), and read-only: the log is written by the audit interceptor alone.
/// </summary>
/// <remarks>
/// Each endpoint names its own policy rather than inheriting one from the group, so a new endpoint
/// here cannot quietly get the wrong one (the lesson of task 6.5.1).
/// </remarks>
public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var audit = app.MapGroup("/api/audit-logs")
            .WithTags("Audit")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        audit.MapGet("/", async (
                [AsParameters] ListAuditLogsQuery query,
                ListAuditLogsHandler handler,
                CancellationToken ct) =>
                    Results.Ok(await handler.Handle(query, ct)))
            .RequireAuthorization(Policies.OwnerOnly)
            .AddEndpointFilter<ValidationFilter<ListAuditLogsQuery>>()
            .WithName("ListAuditLogs")
            .Produces<PagedResponse<AuditLogResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        audit.MapGet("/users", async (ListAuditUsersHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(ct)))
            .RequireAuthorization(Policies.OwnerOnly)
            .WithName("ListAuditUsers")
            .Produces<IReadOnlyList<UserFullName>>();

        return app;
    }
}
