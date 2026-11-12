using Gym.Domain.Common;
using Gym.Domain.Common.Text;

namespace Gym.Domain.Shop;

/// <summary>
/// Something the online shop sells (BUSINESS_RULES.md §16 <i>Products</i>): a name, a description,
/// one category, up to <see cref="MaxPhotos"/> photos and 1 to <see cref="MaxVariants"/> variants, each
/// with its own price and stock; shown on the site or hidden.
/// </summary>
/// <remarks>
/// <para>
/// <b>The aggregate.</b> Variants and photos change only through this class, which checks the rules
/// that span them: how many, labels unique within the product, a label empty only when alone, photo
/// positions 0..n-1 with no gap. The database repeats what it can: positions within range and unique
/// per product, labels unique per product, stock never below zero, a price above zero.
/// </para>
/// <para>
/// <b>Hidden, never deleted</b> (§16, proposed by Claude): a product that was sold is never deleted,
/// so no product is, and "was it sold?" needs no check. A variant may still be removed while the
/// shop has no orders; 16.5 adds the check when orders arrive.
/// </para>
/// </remarks>
public sealed class ShopProduct : Entity
{
    public const int NameMaxLength = 120;

    public const int DescriptionMaxLength = 2000;

    public const int MaxVariants = 20;

    public const int MaxPhotos = 8;

    private readonly List<ShopVariant> _variants = [];

    private readonly List<ShopPhoto> _photos = [];

    // For EF Core.
    private ShopProduct()
    {
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>The name through <see cref="PersianText.Normalize"/>, lower case: unique in the shop.</summary>
    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>Trimmed; null when empty.</summary>
    public string? Description { get; private set; }

    public Guid CategoryId { get; private set; }

    /// <summary>Shown on the site when true (from 16.4); hidden ones stay in the shop admin's list.</summary>
    public bool IsVisible { get; private set; }

    /// <summary>Postgres <c>xmin</c>: a stale edit cannot overwrite a newer one.</summary>
    public uint Version { get; private set; }

    /// <summary>In the order the shop admin listed them.</summary>
    public IReadOnlyList<ShopVariant> Variants => [.. _variants.OrderBy(variant => variant.Position)];

    /// <summary>In their order; the first is the main photo.</summary>
    public IReadOnlyList<ShopPhoto> Photos => [.. _photos.OrderBy(photo => photo.Position)];

    public static Result<ShopProduct> Create(
        string name, string? description, Guid categoryId, bool isVisible, IReadOnlyList<ShopVariantInput> variants)
    {
        var product = new ShopProduct();
        var result = product.Update(name, description, categoryId, isVisible, variants);

        return result.IsSuccess ? product : Result.Failure<ShopProduct>(result.Error);
    }

    /// <summary>
    /// Replaces everything the form holds at once. A variant with an id is changed in place, one
    /// without is added, and one the form no longer lists is removed. Nothing changes unless every
    /// value passes.
    /// </summary>
    public Result Update(
        string name, string? description, Guid categoryId, bool isVisible, IReadOnlyList<ShopVariantInput> variants)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(variants);

        var cleanName = name.Trim();
        if (cleanName.Length == 0)
        {
            return Result.Failure(ShopProductErrors.NameRequired);
        }

        if (cleanName.Length > NameMaxLength)
        {
            return Result.Failure(ShopProductErrors.NameTooLong);
        }

        var cleanDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (cleanDescription?.Length > DescriptionMaxLength)
        {
            return Result.Failure(ShopProductErrors.DescriptionTooLong);
        }

        if (categoryId == Guid.Empty)
        {
            return Result.Failure(ShopProductErrors.CategoryRequired);
        }

        var variantsError = CheckVariants(variants);
        if (variantsError is not null)
        {
            return Result.Failure(variantsError);
        }

        Name = cleanName;
        NormalizedName = PersianText.Normalize(cleanName).ToLowerInvariant();
        Description = cleanDescription;
        CategoryId = categoryId;
        IsVisible = isVisible;
        ReplaceVariants(variants);

        return Result.Success();
    }

    /// <summary>
    /// Adds a photo at the end; the first one becomes the main photo. The caller has re-encoded it and
    /// writes its files under the returned photo's keys.
    /// </summary>
    public Result<ShopPhoto> AddPhoto(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        if (_photos.Count >= MaxPhotos)
        {
            return Result.Failure<ShopPhoto>(ShopProductErrors.TooManyPhotos);
        }

        var photo = ShopPhoto.Create(Id, _photos.Count, width, height);
        _photos.Add(photo);
        return photo;
    }

    /// <summary>Removes a photo and closes the gap; removing the main photo makes the next one main.</summary>
    public Result<ShopPhoto> RemovePhoto(Guid photoId)
    {
        var photo = _photos.SingleOrDefault(p => p.Id == photoId);
        if (photo is null)
        {
            return Result.Failure<ShopPhoto>(ShopProductErrors.PhotoNotFound);
        }

        _photos.Remove(photo);
        Renumber([.. _photos.OrderBy(p => p.Position).Select(p => p.Id)]);
        return photo;
    }

    /// <summary>Puts the photos in the given order; it must name each of them once.</summary>
    public Result ReorderPhotos(IReadOnlyList<Guid> photoIds)
    {
        ArgumentNullException.ThrowIfNull(photoIds);

        if (photoIds.Count != _photos.Count ||
            photoIds.Distinct().Count() != photoIds.Count ||
            photoIds.Any(id => _photos.All(photo => photo.Id != id)))
        {
            return Result.Failure(ShopProductErrors.PhotoOrderInvalid);
        }

        Renumber(photoIds);
        return Result.Success();
    }

    /// <summary>The rules across all variants, then each variant's own.</summary>
    private Error? CheckVariants(IReadOnlyList<ShopVariantInput> variants)
    {
        if (variants.Count == 0)
        {
            return ShopProductErrors.VariantsRequired;
        }

        if (variants.Count > MaxVariants)
        {
            return ShopProductErrors.TooManyVariants;
        }

        foreach (var variant in variants)
        {
            var error = ShopVariant.Check(variant);
            if (error is not null)
            {
                return error;
            }

            if (variant.Id is { } id && _variants.All(existing => existing.Id != id))
            {
                return ShopProductErrors.VariantNotFound;
            }
        }

        if (variants.Count > 1 && variants.Any(variant => string.IsNullOrWhiteSpace(variant.Label)))
        {
            return ShopProductErrors.VariantLabelRequired;
        }

        var labels = variants.Select(variant => ShopVariant.NormalizeLabel(variant.Label)).ToList();
        if (labels.Distinct(StringComparer.Ordinal).Count() != labels.Count)
        {
            return ShopProductErrors.VariantLabelDuplicate;
        }

        // One existing variant listed twice: the form can only do that by mistake.
        var ids = variants.Where(variant => variant.Id is not null).Select(variant => variant.Id!.Value).ToList();
        return ids.Distinct().Count() != ids.Count ? ShopProductErrors.VariantNotFound : null;
    }

    private void ReplaceVariants(IReadOnlyList<ShopVariantInput> variants)
    {
        var kept = variants.Where(variant => variant.Id is not null).Select(variant => variant.Id!.Value).ToHashSet();
        _variants.RemoveAll(existing => !kept.Contains(existing.Id));

        for (var position = 0; position < variants.Count; position++)
        {
            var input = variants[position];
            if (input.Id is { } id)
            {
                _variants.Single(existing => existing.Id == id).Apply(input, position);
            }
            else
            {
                _variants.Add(ShopVariant.Create(Id, input, position));
            }
        }
    }

    private void Renumber(IReadOnlyList<Guid> photoIds)
    {
        for (var position = 0; position < photoIds.Count; position++)
        {
            _photos.Single(photo => photo.Id == photoIds[position]).MoveTo(position);
        }
    }
}
