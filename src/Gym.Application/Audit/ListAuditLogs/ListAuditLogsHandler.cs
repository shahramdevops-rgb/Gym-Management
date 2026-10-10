using System.Text.Json;

using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Domain.Audit;
using Gym.Domain.Auth;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Audit.ListAuditLogs;

/// <summary>
/// The audit screen (گزارش تغییرات): the log's rows, the latest first, with who did it, the member
/// it belongs to and the changed fields (BUSINESS_RULES.md §11 <i>The audit screen</i>, task 11.1).
/// Owner only.
/// </summary>
public sealed class ListAuditLogsHandler(IAppDbContext db, IGymCalendar calendar, IUserNames users)
{
    /// <summary>
    /// The sign-in rows, left out unless asked for: about half the log, and each says only that
    /// someone logged in or stayed logged in.
    /// </summary>
    public static readonly IReadOnlyList<string> SignInEntityTypes = [nameof(RefreshToken), nameof(TrustedDevice)];

    public async Task<PagedResponse<AuditLogResponse>> Handle(ListAuditLogsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var logs = db.AuditLogs;

        // A change's date is the day it happened in the gym's time zone: the days become a range of
        // moments, the end being the midnight after the last day.
        if (query.From is { } from)
        {
            var start = calendar.StartOfDayUtc(from);
            logs = logs.Where(log => log.OccurredAt >= start);
        }

        if (query.To is { } to)
        {
            var end = calendar.StartOfDayUtc(to.AddDays(1));
            logs = logs.Where(log => log.OccurredAt < end);
        }

        if (query.UserId is { } userId)
        {
            logs = logs.Where(log => log.UserId == userId);
        }
        else if (query.SystemOnly)
        {
            logs = logs.Where(log => log.UserId == null);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            logs = logs.Where(log => log.EntityType == query.EntityType);
        }
        else if (!query.IncludeSignIns)
        {
            logs = logs.Where(log => !SignInEntityTypes.Contains(log.EntityType));
        }

        if (!string.IsNullOrWhiteSpace(query.EntityId))
        {
            logs = logs.Where(log => log.EntityId == query.EntityId);
        }

        if (query.Action is { } action)
        {
            logs = logs.Where(log => log.Action == action);
        }

        if (query.MemberId is { } memberId)
        {
            logs = AuditMembers.OfMember(db, logs, memberId);
        }

        var totalCount = await logs.CountAsync(cancellationToken);

        // Id breaks the last tie so paging never repeats or skips a row; it is a version 7 Guid, so
        // it also keeps the order the rows of one save were written in.
        var rows = await logs
            .OrderByDescending(log => log.OccurredAt)
            .ThenByDescending(log => log.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var userIds = rows.Where(row => row.UserId is not null).Select(row => row.UserId!.Value).Distinct().ToList();
        var userNames = await users.FullNamesAsync(userIds, cancellationToken);
        var members = await AuditMembers.ForPageAsync(db, rows, cancellationToken);

        var items = rows
            .Select(row =>
            {
                var member = members.TryGetValue(row.Id, out var found) ? found : ((Guid MemberId, string FullName)?)null;

                return new AuditLogResponse(
                    row.Id,
                    row.OccurredAt,
                    row.Action,
                    row.EntityType,
                    row.EntityId,
                    row.UserId,
                    row.UserId is { } id ? userNames.GetValueOrDefault(id) : null,
                    row.IpAddress,
                    member?.MemberId,
                    member?.FullName,
                    Changes(row));
            })
            .ToList();

        return new PagedResponse<AuditLogResponse>(items, query.Page, query.PageSize, totalCount);
    }

    /// <summary>
    /// The recorded fields, before and after. An update records the same fields on both sides; an
    /// insert has only "after" and a delete only "before". The order is the database's (jsonb keeps
    /// keys sorted its own way), so the page orders them.
    /// </summary>
    private static List<AuditChangeResponse> Changes(AuditLog row)
    {
        var before = Fields(row.OldValues);
        var after = Fields(row.NewValues);

        return before.Keys
            .Concat(after.Keys.Where(field => !before.ContainsKey(field)))
            .Select(field => new AuditChangeResponse(
                field,
                before.TryGetValue(field, out var old) ? old : null,
                after.TryGetValue(field, out var value) ? value : null))
            .ToList();
    }

    private static Dictionary<string, JsonElement> Fields(string? json)
    {
        if (json is null)
        {
            return [];
        }

        // Cloned, so each value outlives the document it was parsed from.
        using var document = JsonDocument.Parse(json);

        return document.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.Clone());
    }
}
