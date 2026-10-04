using Gym.Domain.Common;
using Gym.Domain.Common.Text;

namespace Gym.Domain.Expenses;

/// <summary>
/// What the gym spent money on: "اجاره", "خرید بوفه" (BUSINESS_RULES.md §9).
/// </summary>
/// <remarks>
/// Eight categories arrive with the migration and the Owner adds more. Unlike a cafe category,
/// this one is never deleted and never switched off: expenses point at it and the reports group by
/// it, so the only change it allows is a new name. Renaming is safe because no expense copies the
/// name.
/// </remarks>
public sealed class ExpenseCategory : Entity
{
    public const int NameMaxLength = 100;

    /// <summary>
    /// The seeded «خرید بوفه» (Cafe Purchasing) category. Its id is fixed by the migration, so the
    /// cafe's gross profit (BUSINESS_RULES.md §12 <i>Financial report</i>) finds it even after the
    /// Owner renames it.
    /// </summary>
    public static readonly Guid CafePurchasingId = new("c4e2af4c-deb5-420e-8087-a7780b0356fd");

    // For EF Core.
    private ExpenseCategory()
    {
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// <see cref="Name"/> through <see cref="PersianText.Normalize"/>, in lower case. The unique
    /// index is on this column, so "سایر" typed with an Arabic ye is still a duplicate.
    /// </summary>
    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>Postgres <c>xmin</c>, so a stale rename cannot overwrite a newer one.</summary>
    public uint Version { get; private set; }

    public static Result<ExpenseCategory> Create(string name)
    {
        var category = new ExpenseCategory();
        var result = category.Rename(name);

        return result.IsSuccess ? category : Result.Failure<ExpenseCategory>(result.Error);
    }

    /// <summary>
    /// The normalized form of <paramref name="name"/>, for the seed data, which has to spell the
    /// column out before any entity exists.
    /// </summary>
    public static string Normalize(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return PersianText.Normalize(name.Trim()).ToLowerInvariant();
    }

    public Result Rename(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var cleanName = name.Trim();
        if (cleanName.Length == 0)
        {
            return Result.Failure(ExpenseCategoryErrors.NameRequired);
        }

        if (cleanName.Length > NameMaxLength)
        {
            return Result.Failure(ExpenseCategoryErrors.NameTooLong);
        }

        Name = cleanName;
        NormalizedName = Normalize(cleanName);

        return Result.Success();
    }
}
