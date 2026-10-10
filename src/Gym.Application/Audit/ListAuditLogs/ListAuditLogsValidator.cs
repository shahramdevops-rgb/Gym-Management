using FluentValidation;

using Gym.Application.Common.Paging;

namespace Gym.Application.Audit.ListAuditLogs;

public sealed class ListAuditLogsValidator : AbstractValidator<ListAuditLogsQuery>
{
    public ListAuditLogsValidator()
    {
        RuleFor(query => query.Page).ValidPage();
        RuleFor(query => query.PageSize).ValidPageSize();

        RuleFor(query => query)
            .Must(query => query.From is null || query.To is null || query.From <= query.To)
            .WithErrorCode(AuditErrors.InvalidDateRange.Code)
            .WithMessage(AuditErrors.InvalidDateRange.Description)
            .OverridePropertyName(nameof(ListAuditLogsQuery.To));

        RuleFor(query => query)
            .Must(query => query.UserId is null || !query.SystemOnly)
            .WithErrorCode(AuditErrors.ConflictingUserFilter.Code)
            .WithMessage(AuditErrors.ConflictingUserFilter.Description)
            .OverridePropertyName(nameof(ListAuditLogsQuery.SystemOnly));
    }
}
