using Gym.Domain.Lockers;

namespace Gym.Infrastructure.Persistence.Seed;

/// <summary>
/// The gym's 72 lockers (BUSINESS_RULES.md §6), numbered 1 to <see cref="Locker.Count"/>.
/// </summary>
/// <remarks>
/// <para>
/// Written by the migration (<c>HasData</c> in <c>LockerConfiguration</c>), like
/// <see cref="ExpenseCategorySeed"/> and for the same reasons: they travel with the schema, and
/// nothing in the app creates a locker. The integration tests put the same rows back after each
/// reset, from this same list.
/// </para>
/// <para>
/// The ids are fixed because a migration has to name its rows. Here they are made from the
/// number rather than listed one by one: 72 hand-pasted GUIDs are 72 chances for a typo nobody
/// would notice, while <c>…-000000000012</c> is plainly locker 12. The leading part marks them
/// as this seed's, and the <c>7</c> keeps them looking like the version 7 ids every other row has.
/// </para>
/// </remarks>
public static class LockerSeed
{
    /// <summary>The moment recorded as the seeded rows' <c>CreatedAt</c>: the day task 6.5.5 fixed the lockers.</summary>
    public static readonly DateTimeOffset CreatedAt = new(2026, 9, 27, 0, 0, 0, TimeSpan.Zero);

    public static readonly IReadOnlyList<SeededLocker> All =
        Enumerable.Range(1, Locker.Count).Select(number => new SeededLocker(IdOf(number), number)).ToList();

    /// <summary>The fixed id of locker <paramref name="number"/>.</summary>
    public static Guid IdOf(int number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, Locker.Count);

        return new Guid($"10c4e700-0000-7000-8000-{number:D12}");
    }
}

public sealed record SeededLocker(Guid Id, int Number);
