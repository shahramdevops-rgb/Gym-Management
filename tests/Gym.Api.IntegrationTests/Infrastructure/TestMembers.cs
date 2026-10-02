using Gym.Domain.Members;

namespace Gym.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Members seeded as a precondition for testing something else — a subscription, a payment, a
/// check-in — rather than to test the member itself.
/// </summary>
internal static class TestMembers
{
    /// <summary>
    /// The gym's today for a seeded member. <see cref="Member.Create"/> asks for it because a
    /// birth date can only be judged against a date, so the value is named once here instead of a
    /// dozen tests each reaching for a clock they do not need.
    /// </summary>
    private static readonly DateOnly SeedToday = new(2026, 1, 1);

    /// <summary>
    /// Every member has a birth date (BUSINESS_RULES.md §2). Tests about something else get this one.
    /// </summary>
    private static readonly DateOnly SeedBirthDate = new(1990, 6, 15);

    /// <summary>An active member with no notes and the default birth date.</summary>
    internal static Member Seed(string fullName, string phoneNumber) =>
        Member.Create(fullName, phoneNumber, notes: null, SeedBirthDate, today: SeedToday).Value;

    /// <summary>An active member with no notes and the given birth date.</summary>
    internal static Member Seed(string fullName, string phoneNumber, DateOnly birthDate) =>
        Member.Create(fullName, phoneNumber, notes: null, birthDate, today: SeedToday).Value;
}
