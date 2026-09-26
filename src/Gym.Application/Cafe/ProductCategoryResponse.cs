using System.Linq.Expressions;

using Gym.Domain.Cafe;

namespace Gym.Application.Cafe;

/// <param name="IsActive">موجود when true; a category that is switched off sells nothing under it.</param>
/// <param name="Version">Sent back with an update, so a stale edit is refused.</param>
public sealed record ProductCategoryResponse(
    Guid Id,
    string Name,
    bool IsActive,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt)
{
    /// <summary>The same mapping as <see cref="From"/>, as an expression EF Core translates to SQL.</summary>
    public static readonly Expression<Func<ProductCategory, ProductCategoryResponse>> Projection =
        category => new ProductCategoryResponse(
            category.Id,
            category.Name,
            category.IsActive,
            category.Version,
            category.CreatedAt,
            category.UpdatedAt);

    public static ProductCategoryResponse From(ProductCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);

        return new ProductCategoryResponse(
            category.Id,
            category.Name,
            category.IsActive,
            category.Version,
            category.CreatedAt,
            category.UpdatedAt);
    }
}
