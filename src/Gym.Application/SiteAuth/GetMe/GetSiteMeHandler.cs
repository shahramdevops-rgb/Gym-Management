using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.SiteAuth;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.SiteAuth.GetMe;

/// <summary>
/// The signed-in member or trainer, read from the database on every call, so a deactivation at the
/// desk shows as the «غیرفعال» notice at once rather than when the access token runs out, and a
/// stopped trainer's area closes at once (BUSINESS_RULES.md §15).
/// </summary>
public sealed class GetSiteMeHandler(IAppDbContext db)
{
    public async Task<Result<SiteMeResponse>> Handle(Guid siteAccountId, CancellationToken cancellationToken)
    {
        var account = await db.SiteAccounts.AsNoTracking().SingleOrDefaultAsync(row => row.Id == siteAccountId, cancellationToken);
        if (account is null)
        {
            return Result.Failure<SiteMeResponse>(SiteAuthErrors.AccountNotFound);
        }

        return SiteMeResponse.From(await SitePerson.LoadAsync(db, account, cancellationToken));
    }
}
