using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Members;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Members.GetMember;

/// <summary>One member, active or not, with their site account if they have one (BUSINESS_RULES.md §15).</summary>
public sealed class GetMemberHandler(IAppDbContext db)
{
    public async Task<Result<MemberResponse>> Handle(Guid id, CancellationToken cancellationToken)
    {
        // A read changes nothing, so nothing needs tracking: select the response straight from SQL.
        var member = await db.Members
            .AsNoTracking()
            .Where(m => m.Id == id)
            .Select(MemberResponse.Projection)
            .SingleOrDefaultAsync(cancellationToken);

        if (member is null)
        {
            return Result.Failure<MemberResponse>(MemberErrors.NotFound);
        }

        var siteAccount = await db.SiteAccounts
            .AsNoTracking()
            .Where(account => account.MemberId == id)
            .Select(account => new MemberSiteAccount(account.CreatedAt, account.LastSignedInAt))
            .SingleOrDefaultAsync(cancellationToken);

        return member with { SiteAccount = siteAccount };
    }
}
