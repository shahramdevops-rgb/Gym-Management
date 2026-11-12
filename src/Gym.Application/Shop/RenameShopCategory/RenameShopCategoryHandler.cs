using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Shop;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Shop.RenameShopCategory;

/// <summary>Renames a shop category. Shop admin only; the version refuses a stale form.</summary>
public sealed class RenameShopCategoryHandler(IAppDbContext db)
{
    public async Task<Result<ShopCategoryResponse>> Handle(
        Guid id, RenameShopCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await db.ShopCategories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return Result.Failure<ShopCategoryResponse>(ShopCategoryErrors.NotFound);
        }

        if (category.Version != command.Version)
        {
            return Result.Failure<ShopCategoryResponse>(ShopCategoryErrors.ChangedConcurrently);
        }

        var renamed = category.Rename(command.Name);
        if (renamed.IsFailure)
        {
            return Result.Failure<ShopCategoryResponse>(renamed.Error);
        }

        var normalizedName = category.NormalizedName;
        if (await db.ShopCategories.AnyAsync(c => c.NormalizedName == normalizedName && c.Id != id, cancellationToken))
        {
            return Result.Failure<ShopCategoryResponse>(ShopCategoryErrors.NameAlreadyExists);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception)
            when (exception.ConstraintName == ShopCatalogConstraints.UniqueCategoryName)
        {
            return Result.Failure<ShopCategoryResponse>(ShopCategoryErrors.NameAlreadyExists);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ShopCategoryResponse>(ShopCategoryErrors.ChangedConcurrently);
        }

        var productCount = await db.ShopProducts.CountAsync(p => p.CategoryId == id, cancellationToken);
        return ShopCategoryResponse.From(category, productCount);
    }
}
