using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Members;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Members.SetMemberActive;

/// <summary>
/// Deactivates or reactivates a member (BUSINESS_RULES.md §2: deactivated, never deleted).
/// </summary>
/// <remarks>
/// Repeating either action succeeds and changes nothing: EF Core sees no modified property, so
/// nothing is saved and no audit row is written. No <c>Version</c> is asked of the client,
/// unlike an edit: the request carries no data that could have been read stale, only the
/// intent. <c>xmin</c> still refuses a save that races another one.
/// </remarks>
public sealed class SetMemberActiveHandler(IAppDbContext db)
{
    public Task<Result<MemberResponse>> Deactivate(Guid id, CancellationToken cancellationToken) =>
        Change(id, member => member.Deactivate(), cancellationToken);

    public Task<Result<MemberResponse>> Reactivate(Guid id, CancellationToken cancellationToken) =>
        Change(id, member => member.Reactivate(), cancellationToken);

    private async Task<Result<MemberResponse>> Change(Guid id, Action<Member> change, CancellationToken cancellationToken)
    {
        var member = await db.Members.SingleOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (member is null)
        {
            return Result.Failure<MemberResponse>(MemberErrors.NotFound);
        }

        change(member);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<MemberResponse>(MemberErrors.ChangedConcurrently);
        }

        return MemberResponse.From(member);
    }
}
