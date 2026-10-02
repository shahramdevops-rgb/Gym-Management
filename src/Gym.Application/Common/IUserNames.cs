namespace Gym.Application.Common;

/// <summary>
/// The full names of the people who recorded things, for lists that say who did it
/// (BUSINESS_RULES.md §12 <i>History</i>).
/// </summary>
/// <remarks>
/// Declared here and implemented in Infrastructure, because Application cannot see the Identity
/// <c>User</c> (ADR 0002). One call per page of rows, never one per row.
/// </remarks>
public interface IUserNames
{
    /// <summary>
    /// The full name of each id that names a user. An id with no user is missing from the result,
    /// so callers read it with a default.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> FullNamesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}
