using Gym.Application.Common;

namespace Gym.Application.Audit.ListAuditUsers;

/// <summary>
/// Everyone who could appear in the audit log, for its "who" filter and to name the users a change
/// records (BUSINESS_RULES.md §11 <i>The audit screen</i>). Owner only.
/// </summary>
/// <remarks>
/// Not paged, unlike every other list: it is the gym's accounts, one Owner and a few staff, and the
/// filter has to offer all of them at once. Deactivated staff are included, because what they did
/// is still in the log.
/// </remarks>
public sealed class ListAuditUsersHandler(IUserNames users)
{
    public Task<IReadOnlyList<UserFullName>> Handle(CancellationToken cancellationToken) =>
        users.ListAllAsync(cancellationToken);
}
