using System.Text.Json;
using System.Text.Json.Serialization;

using Gym.Domain.Audit;

namespace Gym.Application.Audit.ListAuditLogs;

/// <summary>
/// One row of the audit screen: who did what to which record, when and from where, with the member
/// it belongs to and the changed fields (BUSINESS_RULES.md §11 <i>The audit screen</i>).
/// </summary>
/// <param name="EntityType">The record's kind by its name in the model; the page shows it in Persian.</param>
/// <param name="EntityId">The record's key as the log wrote it; with the kind, it filters to the record's history.</param>
/// <param name="UserFullName"><c>null</c> for the system's rows, and for a user id that names no account.</param>
/// <param name="MemberId">The member the record belongs to, when it belongs to one.</param>
/// <param name="Changes">
/// Every field the log recorded. The values are exactly as stored, and the page orders and formats
/// them: the screen decides what a field means, the log only keeps it.
/// </param>
public sealed record AuditLogResponse(
    Guid Id,
    DateTimeOffset OccurredAt,
    [property: JsonConverter(typeof(JsonStringEnumConverter<AuditAction>))] AuditAction Action,
    string EntityType,
    string EntityId,
    Guid? UserId,
    string? UserFullName,
    string? IpAddress,
    Guid? MemberId,
    string? MemberFullName,
    IReadOnlyList<AuditChangeResponse> Changes);

/// <summary>One field: its value before and after.</summary>
/// <param name="Field">The property's name in the model, for example <c>UsedSessions</c>.</param>
/// <param name="OldValue"><c>null</c> for an insert, which has nothing before it.</param>
/// <param name="NewValue"><c>null</c> for a delete, which has nothing after it.</param>
public sealed record AuditChangeResponse(string Field, JsonElement? OldValue, JsonElement? NewValue);
