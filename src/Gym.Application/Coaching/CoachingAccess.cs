using Gym.Application.Common;
using Gym.Application.SiteAuth;
using Gym.Domain.Coaching;
using Gym.Domain.Common;
using Gym.Domain.Members;
using Gym.Domain.SiteAuth;
using Gym.Domain.Trainers;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Coaching;

/// <summary>
/// Who is asking, read from the database at each call. The site's policies read the roles in the
/// access token, which may be up to 15 minutes old; a member the desk deactivated, or a trainer the
/// Owner stopped, is refused here at once (BUSINESS_RULES.md §15).
/// </summary>
internal static class CoachingAccess
{
    /// <summary>The member's area: the account's member, while active.</summary>
    public static async Task<Result<Member>> MemberAsync(IAppDbContext db, Guid siteAccountId, CancellationToken cancellationToken)
    {
        var person = await PersonAsync(db, siteAccountId, cancellationToken);
        if (person is null)
        {
            return Result.Failure<Member>(SiteAuthErrors.AccountNotFound);
        }

        return person.IsMember ? person.Member! : Result.Failure<Member>(CoachingErrors.MemberInactive);
    }

    /// <summary>The trainer's area: the account's trainer, while a trainer now.</summary>
    public static async Task<Result<Trainer>> TrainerAsync(IAppDbContext db, Guid siteAccountId, CancellationToken cancellationToken)
    {
        var person = await PersonAsync(db, siteAccountId, cancellationToken);
        if (person is null)
        {
            return Result.Failure<Trainer>(SiteAuthErrors.AccountNotFound);
        }

        return person.IsTrainer ? person.Trainer! : Result.Failure<Trainer>(CoachingErrors.NotATrainer);
    }

    private static async Task<SitePerson?> PersonAsync(IAppDbContext db, Guid siteAccountId, CancellationToken cancellationToken)
    {
        var account = await db.SiteAccounts.AsNoTracking().SingleOrDefaultAsync(row => row.Id == siteAccountId, cancellationToken);

        return account is null ? null : await SitePerson.LoadAsync(db, account, cancellationToken);
    }
}
