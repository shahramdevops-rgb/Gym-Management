using Gym.Application.Common.Paging;
using Gym.Domain.Audit;

namespace Gym.Application.Audit.ListAuditLogs;

/// <summary>
/// Bound from the query string:
/// <c>GET /api/audit-logs?from=2026-10-10&amp;to=2026-10-10&amp;entityType=Subscription&amp;page=1</c>.
/// Every filter is optional; together they narrow the list (BUSINESS_RULES.md §11 <i>The audit screen</i>).
/// </summary>
/// <param name="From">
/// Inclusive, compared with the day the change happened in the gym's time zone. <c>null</c> means no
/// lower bound. The page sends today when the Owner has chosen nothing.
/// </param>
/// <param name="To">Inclusive. <c>null</c> means no upper bound.</param>
/// <param name="UserId">Only what this user did.</param>
/// <param name="SystemOnly">Only the rows with no user: logins, seeding, background jobs.</param>
/// <param name="EntityType">One kind of record, by its name in the model, for example <c>Subscription</c>.</param>
/// <param name="EntityId">One record, by its key as the log writes it: the history of that record.</param>
/// <param name="Action">Only inserts, only updates or only deletes.</param>
/// <param name="MemberId">The rows that belong to this member (see <see cref="AuditMembers"/>).</param>
/// <param name="IncludeSignIns">
/// Also the refresh token and trusted device rows, which are left out by default. Choosing one of
/// those kinds as <paramref name="EntityType"/> shows them anyway.
/// </param>
public sealed record ListAuditLogsQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    Guid? UserId = null,
    bool SystemOnly = false,
    string? EntityType = null,
    string? EntityId = null,
    AuditAction? Action = null,
    Guid? MemberId = null,
    bool IncludeSignIns = false,
    int Page = 1,
    int PageSize = PagingRules.DefaultPageSize);
