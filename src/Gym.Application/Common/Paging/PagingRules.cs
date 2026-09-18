using FluentValidation;

namespace Gym.Application.Common.Paging;

/// <summary>The paging limits every list shares, as FluentValidation rules.</summary>
public static class PagingRules
{
    public const int DefaultPageSize = 20;

    /// <summary>docs/ARCHITECTURE.md: at most 100 rows per page.</summary>
    public const int MaxPageSize = 100;

    public static IRuleBuilderOptions<T, int> ValidPage<T>(this IRuleBuilder<T, int> rule) =>
        rule.GreaterThanOrEqualTo(1).WithErrorCode("Paging.PageInvalid").WithMessage("Page must be 1 or more.");

    public static IRuleBuilderOptions<T, int> ValidPageSize<T>(this IRuleBuilder<T, int> rule) =>
        rule.InclusiveBetween(1, MaxPageSize)
            .WithErrorCode("Paging.PageSizeInvalid")
            .WithMessage($"Page size must be between 1 and {MaxPageSize}.");
}
