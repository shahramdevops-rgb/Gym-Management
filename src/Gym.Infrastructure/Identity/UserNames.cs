using Gym.Application.Common;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Gym.Infrastructure.Identity;

/// <summary><see cref="IUserNames"/> read straight from Identity's <c>users</c> table.</summary>
/// <remarks>
/// A read, so no <c>UserManager</c>: it has no batch lookup, and one query for a whole page is the
/// point. Deactivated users keep their name here, because what they recorded is still theirs.
/// </remarks>
public sealed class UserNames(AppDbContext db) : IUserNames
{
    public async Task<IReadOnlyDictionary<Guid, string>> FullNamesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await db.Users.AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);
    }
}
