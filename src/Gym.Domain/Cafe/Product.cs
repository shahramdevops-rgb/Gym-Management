using Gym.Domain.Common;
using Gym.Domain.Common.Text;

namespace Gym.Domain.Cafe;

/// <summary>
/// A line on the cafe's price list: "آب معدنی، ۱۵٬۰۰۰" (BUSINESS_RULES.md §8).
/// Products are deactivated, never deleted.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no stock.</b> The Owner decided on 1405/07/03 that the gym does not count what is
/// in the fridge, so this class has no quantity, and selling never asks whether anything is left.
/// A number nobody maintains is worse than no number.
/// </para>
/// <para>
/// An order copies the product's name and price when it is sold (§8), the same way a subscription
/// copies its plan's, so editing a price never reaches an order already rung up. That is what
/// makes <see cref="Update"/> safe to offer at the front desk.
/// </para>
/// </remarks>
public sealed class Product : Entity
{
    public const int NameMaxLength = 100;

    /// <summary>The column is <c>numeric(18,2)</c>: 16 digits before the point, 2 after.</summary>
    public const int PriceDecimals = 2;

    public const decimal MaxPrice = 9_999_999_999_999_999.99m;

    // For EF Core.
    private Product()
    {
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// <see cref="Name"/> through <see cref="PersianText.Normalize"/>, in lower case. The unique
    /// index is on this column, and it covers the whole cafe rather than one category.
    /// </summary>
    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>
    /// The category this product is listed under. Whether that category exists is a question about
    /// another table, so the handler checks it and the foreign key is the safety net.
    /// </summary>
    public Guid CategoryId { get; private set; }

    public decimal Price { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Postgres <c>xmin</c>, so a stale edit cannot overwrite a newer one.</summary>
    public uint Version { get; private set; }

    public static Result<Product> Create(string name, Guid categoryId, decimal price)
    {
        var product = new Product { IsActive = true };
        var result = product.Update(name, categoryId, price);

        return result.IsSuccess ? product : Result.Failure<Product>(result.Error);
    }

    /// <summary>
    /// Replaces the product's details, including its category. Allowed while inactive too, so a
    /// discontinued item can be corrected before it is offered again. Orders already rung up keep
    /// their own copy of the name and price (BUSINESS_RULES.md §8).
    /// </summary>
    public Result Update(string name, Guid categoryId, decimal price)
    {
        ArgumentNullException.ThrowIfNull(name);

        var cleanName = name.Trim();
        if (cleanName.Length == 0)
        {
            return Result.Failure(ProductErrors.NameRequired);
        }

        if (cleanName.Length > NameMaxLength)
        {
            return Result.Failure(ProductErrors.NameTooLong);
        }

        if (categoryId == Guid.Empty)
        {
            return Result.Failure(ProductErrors.CategoryRequired);
        }

        var priceError = CheckPrice(price);
        if (priceError is not null)
        {
            return Result.Failure(priceError);
        }

        Name = cleanName;
        NormalizedName = PersianText.Normalize(cleanName).ToLowerInvariant();
        CategoryId = categoryId;
        Price = price;

        return Result.Success();
    }

    /// <summary>Deactivating an inactive product succeeds and changes nothing.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Activating an active product succeeds and changes nothing.</summary>
    public void Activate() => IsActive = true;

    /// <summary>
    /// BUSINESS_RULES.md §8: an inactive product is not sold. The use case that sells one
    /// (task 7.2) asks this instead of reading <see cref="IsActive"/> itself, so the rule has
    /// one home — the same shape as <c>Plan.EnsureCanBeSold</c>.
    /// </summary>
    public Result EnsureCanBeSold() => IsActive ? Result.Success() : Result.Failure(ProductErrors.Inactive);

    /// <summary>
    /// The same money rule as <see cref="Plans.Plan.CheckPrice"/>, with this feature's own error
    /// codes so the Persian message names the field the user was typing into. Also used by the
    /// Application validators, so the form hears the same answer as the entity. Too many decimals
    /// is refused rather than rounded: rounding money silently is how "15,000.005" becomes a price
    /// nobody typed.
    /// </summary>
    public static Error? CheckPrice(decimal price)
    {
        if (price < 0)
        {
            return ProductErrors.PriceNegative;
        }

        if (price > MaxPrice)
        {
            return ProductErrors.PriceTooLarge;
        }

        return decimal.Round(price, PriceDecimals) != price ? ProductErrors.PriceTooManyDecimals : null;
    }
}
