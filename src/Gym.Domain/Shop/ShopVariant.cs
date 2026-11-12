using Gym.Domain.Common;
using Gym.Domain.Common.Text;

namespace Gym.Domain.Shop;

/// <summary>
/// One thing a customer can buy: «شکلاتی، ۲ کیلو» of a protein, with its own price and stock
/// (BUSINESS_RULES.md §16 <i>Products</i>: a plain label, decided by the developer 1405/08/21).
/// A product without variants is one variant whose label may be empty.
/// </summary>
/// <remarks>
/// Part of <see cref="ShopProduct"/>: created, changed and removed only through it, which checks the
/// rules that span variants (how many, labels unique, empty only when alone).
/// </remarks>
public sealed class ShopVariant : Entity
{
    public const int LabelMaxLength = 60;

    public const int PriceDecimals = 2;

    /// <summary>The largest value <c>numeric(18,2)</c> holds.</summary>
    public const decimal MaxPrice = 9_999_999_999_999_999.99m;

    /// <summary>Typed by hand; a ceiling so a slip of the finger is caught.</summary>
    public const int MaxStock = 999_999;

    // For EF Core.
    private ShopVariant()
    {
    }

    public Guid ProductId { get; private set; }

    /// <summary>Trimmed; empty only on a product with a single variant.</summary>
    public string Label { get; private set; } = string.Empty;

    /// <summary>The label through <see cref="PersianText.Normalize"/>, lower case: the uniqueness key.</summary>
    public string NormalizedLabel { get; private set; } = string.Empty;

    /// <summary>Toman, like every amount in the app (§0).</summary>
    public decimal Price { get; private set; }

    /// <summary>Typed by the shop admin; never below zero (a check constraint too).</summary>
    public int Stock { get; private set; }

    /// <summary>The order the shop admin listed them in, from 0.</summary>
    public int Position { get; private set; }

    internal static ShopVariant Create(Guid productId, ShopVariantInput input, int position)
    {
        var variant = new ShopVariant { ProductId = productId };
        variant.Apply(input, position);
        return variant;
    }

    /// <summary>The product has already checked every value with <see cref="Check"/>.</summary>
    internal void Apply(ShopVariantInput input, int position)
    {
        Label = input.Label.Trim();
        NormalizedLabel = NormalizeLabel(input.Label);
        Price = input.Price;
        Stock = input.Stock;
        Position = position;
    }

    internal static string NormalizeLabel(string label) => PersianText.Normalize(label.Trim()).ToLowerInvariant();

    /// <summary>The checks one variant can make alone; the product checks the rest.</summary>
    public static Error? Check(ShopVariantInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Label);

        if (input.Label.Trim().Length > LabelMaxLength)
        {
            return ShopProductErrors.VariantLabelTooLong;
        }

        var priceError = CheckPrice(input.Price);
        if (priceError is not null)
        {
            return priceError;
        }

        return input.Stock is < 0 or > MaxStock ? ShopProductErrors.VariantStockOutOfRange : null;
    }

    public static Error? CheckPrice(decimal price)
    {
        if (price <= 0)
        {
            return ShopProductErrors.VariantPriceNotPositive;
        }

        if (price > MaxPrice)
        {
            return ShopProductErrors.VariantPriceTooLarge;
        }

        return decimal.Round(price, PriceDecimals) != price ? ShopProductErrors.VariantPriceTooManyDecimals : null;
    }
}

/// <summary>
/// A variant as the form sends it: <see cref="Id"/> names an existing one to change, null adds one.
/// </summary>
public sealed record ShopVariantInput(Guid? Id, string Label, decimal Price, int Stock);
