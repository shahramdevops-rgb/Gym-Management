using Gym.Domain.Expenses;

namespace Gym.Infrastructure.Persistence.Seed;

/// <summary>
/// The eight expense categories every database starts with (BUSINESS_RULES.md §9).
/// </summary>
/// <remarks>
/// <para>
/// They are written by the migration (<c>HasData</c> in <c>ExpenseCategoryConfiguration</c>), not
/// by a seeder at start-up. Reference data then travels with the schema: it is applied exactly
/// once, on the same deployment step as the table it fills, and a category the Owner renames is
/// never put back by a seeder that finds its old name missing.
/// </para>
/// <para>
/// The ids are fixed because a migration has to name its rows, and because a later migration can
/// then point at "خرید بوفه" without looking it up by a name the Owner may have changed. The
/// integration tests put the same rows back after each reset, from this same list.
/// </para>
/// </remarks>
public static class ExpenseCategorySeed
{
    /// <summary>The moment recorded as the seeded rows' <c>CreatedAt</c>: the day task 8.1 added them.</summary>
    public static readonly DateTimeOffset CreatedAt = new(2026, 9, 27, 0, 0, 0, TimeSpan.Zero);

    public static readonly IReadOnlyList<SeededExpenseCategory> All =
    [
        new(new Guid("0b3f6685-b1a2-4979-acd9-c1fe848f156b"), "اجاره"),
        new(new Guid("5e5cf0e4-0d67-4b55-830a-8d0a6919fd00"), "حقوق"),
        new(new Guid("b74beca9-8e56-440f-9a19-e3ab97690134"), "برق"),
        new(new Guid("627d2be7-d824-422a-ba2c-8d17c98633b5"), "آب"),
        new(new Guid("270c9d15-6962-4eae-a492-483541b8d516"), "تجهیزات"),
        new(new Guid("97813c23-681f-4e8e-84eb-fa8ecd73c3ac"), "تعمیر و نگهداری"),
        new(ExpenseCategory.CafePurchasingId, "خرید بوفه"),
        new(new Guid("1dfd1cc7-0587-4096-a384-73e0449b23cd"), "سایر"),
    ];
}

/// <param name="Name">The Persian name, as the Owner sees it.</param>
public sealed record SeededExpenseCategory(Guid Id, string Name);
