using Gym.Domain.Common;

namespace Gym.Application.Audit;

/// <summary>The audit screen's refusals (BUSINESS_RULES.md §11 <i>The audit screen</i>).</summary>
public static class AuditErrors
{
    public static readonly Error InvalidDateRange = Error.Validation(
        "Audit.InvalidDateRange",
        "The start of the date range is after its end.");

    public static readonly Error ConflictingUserFilter = Error.Validation(
        "Audit.ConflictingUserFilter",
        "Filter by one user or by the system's rows, not both.");
}
