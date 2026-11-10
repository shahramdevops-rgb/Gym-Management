namespace Gym.Application.Coaching;

/// <summary>
/// «دیدم» on «خبر مربی» or «خبر شاگردان» (task 15.4d): the items the card showed, by their
/// <see cref="CoachingNewsItemResponse.Id"/>. What came after the card was read is not among them,
/// so it stays to be seen. An id that is not the caller's news is passed over.
/// </summary>
public sealed record SeeCoachingNewsCommand(IReadOnlyList<Guid>? Ids)
{
    /// <summary>More than a card ever holds: the rest of a longer list is passed over.</summary>
    internal const int MaxIds = 100;

    internal List<Guid> DistinctIds() => (Ids ?? []).Distinct().Take(MaxIds).ToList();
}
