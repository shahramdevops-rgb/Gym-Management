using System.Linq.Expressions;

using Gym.Domain.Cafe;

namespace Gym.Application.Cafe;

/// <summary>
/// The join every read of a product needs: the product plus its category's name and switch. Kept
/// in one place so the single-product read and the list cannot drift apart.
/// </summary>
/// <remarks>
/// An expression rather than <c>ProductResponse.From</c>, because EF Core has to translate it to
/// SQL. Written as a join instead of a navigation property: the entity holds
/// <c>CategoryId</c> only, which keeps Domain free of the object graph EF would otherwise manage.
/// </remarks>
public static class ProductWithCategory
{
    public static readonly Expression<Func<Product, ProductCategory, ProductResponse>> Projection =
        (product, category) => new ProductResponse(
            product.Id,
            product.Name,
            product.CategoryId,
            category.Name,
            product.Price,
            product.IsActive,
            category.IsActive,
            product.Version,
            product.CreatedAt,
            product.UpdatedAt);
}
