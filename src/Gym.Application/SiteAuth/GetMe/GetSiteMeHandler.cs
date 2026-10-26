using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.SiteAuth;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.SiteAuth.GetMe;

/// <summary>
/// The signed-in member, read from the database on every call, so a deactivation at the desk shows
/// as the «غیرفعال» notice at once rather than when the access token runs out (BUSINESS_RULES.md §15).
/// </summary>
public sealed class GetSiteMeHandler(IAppDbContext db)
{
    public async Task<Result<SiteMeResponse>> Handle(Guid siteAccountId, CancellationToken cancellationToken)
    {
        var member = await db.SiteAccounts
            .Where(account => account.Id == siteAccountId)
            .Join(db.Members, account => account.MemberId, member => member.Id, (_, member) => member)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        return member is null
            ? Result.Failure<SiteMeResponse>(SiteAuthErrors.AccountNotFound)
            : SiteMeResponse.From(member);
    }
}
