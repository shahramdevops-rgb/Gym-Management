using Gym.Domain.Common;
using Gym.Domain.Common.Text;

namespace Gym.Domain.Cafe;

/// <summary>
/// A heading on the cafe's price list: "نوشیدنی", "مکمل" (BUSINESS_RULES.md §8).
/// </summary>
/// <remarks>
/// <para>
/// A category carries no money and no history, which is why it is the one thing in the cafe that
/// can be deleted at all — but only while no product points at it. Whether it is empty is a
/// question about another table, so the handler answers it and the foreign key is the safety net;
/// this class enforces only what it can see on its own row, the same division
/// <see cref="Lockers.Locker"/> uses for occupancy.
/// </para>
/// <para>
/// <see cref="IsActive"/> is the whole shelf's switch, and it reads as موجود / ناموجود on screen:
/// the gym counts no quantities, so this flag is the only thing that says whether the things
/// under this heading can be bought today. Switching a category off takes its products out of the
/// till with it (BUSINESS_RULES.md §8), which is a question about the other table again — the
/// handlers that list products answer it, not this class.
/// </remarks>
public sealed class ProductCategory : Entity
{
    public const int NameMaxLength = 100;

    // For EF Core.
    private ProductCategory()
    {
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// <see cref="Name"/> through <see cref="PersianText.Normalize"/>, in lower case. The unique
    /// index is on this column, so "نوشیدنی" typed with an Arabic ye is still a duplicate.
    /// </summary>
    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>موجود when true. A category that is switched off sells nothing under it.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Postgres <c>xmin</c>, so a stale edit cannot overwrite a newer one.</summary>
    public uint Version { get; private set; }

    public static Result<ProductCategory> Create(string name)
    {
        var category = new ProductCategory { IsActive = true };
        var result = category.Rename(name);

        return result.IsSuccess ? category : Result.Failure<ProductCategory>(result.Error);
    }

    /// <summary>
    /// Renaming is safe at any time: an order snapshots the product's name, not its category's,
    /// and nothing else copies this value.
    /// </summary>
    public Result Rename(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var cleanName = name.Trim();
        if (cleanName.Length == 0)
        {
            return Result.Failure(ProductCategoryErrors.NameRequired);
        }

        if (cleanName.Length > NameMaxLength)
        {
            return Result.Failure(ProductCategoryErrors.NameTooLong);
        }

        Name = cleanName;
        NormalizedName = PersianText.Normalize(cleanName).ToLowerInvariant();

        return Result.Success();
    }

    /// <summary>Switching off an already inactive category succeeds and changes nothing.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Switching on an already active category succeeds and changes nothing.</summary>
    public void Activate() => IsActive = true;
}
