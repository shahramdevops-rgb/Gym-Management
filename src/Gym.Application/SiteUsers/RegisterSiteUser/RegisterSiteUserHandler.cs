using Gym.Application.Common;
using Gym.Application.Members;
using Gym.Domain.Common;
using Gym.Domain.Members;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.SiteUsers.RegisterSiteUser;

/// <summary>
/// «ثبت به‌عنوان عضو» on the new-member form (BUSINESS_RULES.md §16 <i>Site users</i>): the desk
/// takes in someone who signed up on the site. From then on they are a member like any other, in
/// every list and search, before any plan; what they entered on the site is kept, and the desk
/// corrects it on the member page if it needs to (the developer, 1405/08/19).
/// </summary>
/// <remarks>
/// Asked only of a site user: someone already a member is "not found" here, so the form cannot
/// reach a member it was never meant to. No <c>Version</c> is asked, as with deactivating: the
/// request carries no data that could have been read stale. The audit log records the change.
/// </remarks>
public sealed class RegisterSiteUserHandler(IAppDbContext db)
{
    public async Task<Result<MemberResponse>> Handle(Guid id, CancellationToken cancellationToken)
    {
        var member = await db.Members
            .Where(m => m.Id == id)
            .Where(SiteUserRule.IsSiteUser(db))
            .SingleOrDefaultAsync(cancellationToken);

        if (member is null)
        {
            return Result.Failure<MemberResponse>(MemberErrors.SiteUserNotFound);
        }

        member.RegisterAtDesk();

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
