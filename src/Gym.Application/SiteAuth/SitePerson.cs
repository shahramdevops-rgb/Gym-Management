using Gym.Application.Common;
using Gym.Domain.Members;
using Gym.Domain.SiteAuth;
using Gym.Domain.Trainers;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.SiteAuth;

/// <summary>
/// Who a site account is right now: the member, the trainer, or both (BUSINESS_RULES.md §15). Read
/// from the database at every sign-in, refresh and «me», so the roles in a token are never older
/// than the access token's 15 minutes.
/// </summary>
/// <param name="Trainer">The linked trainer, stopped or not; <see cref="IsTrainer"/> says which.</param>
public sealed record SitePerson(Member? Member, Trainer? Trainer)
{
    /// <summary>
    /// The member's area opens only for an active member: a member the desk deactivated sees the
    /// «غیرفعال» notice through «me» and nothing else (BUSINESS_RULES.md §15).
    /// </summary>
    public bool IsMember => Member is { IsActive: true };

    /// <summary>The trainer's area opens only while they are a trainer.</summary>
    public bool IsTrainer => Trainer is { IsActive: true };

    public static async Task<SitePerson> LoadAsync(IAppDbContext db, SiteAccount account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(account);

        var member = account.MemberId is { } memberId
            ? await db.Members.SingleAsync(row => row.Id == memberId, cancellationToken)
            : null;

        var trainer = account.TrainerId is { } trainerId
            ? await db.Trainers.Include(row => row.Periods).SingleAsync(row => row.Id == trainerId, cancellationToken)
            : null;

        return new SitePerson(member, trainer);
    }
}
