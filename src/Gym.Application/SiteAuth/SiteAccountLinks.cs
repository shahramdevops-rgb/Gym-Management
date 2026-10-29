using Gym.Application.Common;
using Gym.Domain.Auth;
using Gym.Domain.Members;
using Gym.Domain.SiteAuth;
using Gym.Domain.Trainers;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.SiteAuth;

/// <summary>
/// The account a verified phone signs in to, found or made, with its links set to who the phone
/// belongs to now (BUSINESS_RULES.md §15): the member with that phone, the trainer with that phone,
/// or both. Shared by the sign-in and the sign-up. Called with the phone locked, inside the
/// caller's transaction; the caller saves.
/// </summary>
/// <remarks>
/// <para>
/// <b>The member's account comes first.</b> A member keeps one account for good, so when the phone
/// is a member's, that account is used, and the trainer link moves to it. A trainer-only account
/// takes the member in when the member is new to the site.
/// </para>
/// <para>
/// <b>Another member's account never opens for the trainer.</b> If the trainer's link sits on the
/// account of a member whose phone the desk has since changed, the link leaves that account, and
/// the trainer gets an account of their own.
/// </para>
/// <para>
/// One save moves a trainer link from one account to another: EF Core orders the update that
/// frees the unique value before the one that takes it.
/// </para>
/// </remarks>
public sealed class SiteAccountLinks(IAppDbContext db)
{
    /// <param name="member">The member with the phone, if any; may be new and not saved yet.</param>
    /// <param name="trainer">The trainer with the phone, if any, stopped or not.</param>
    public async Task<SiteAccount> FindOrMakeAsync(
        Member? member,
        Trainer? trainer,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (member is null && trainer is null)
        {
            throw new ArgumentException("A site account is a member's or a trainer's.", nameof(member));
        }

        var memberAccount = member is null
            ? null
            : await db.SiteAccounts.SingleOrDefaultAsync(row => row.MemberId == member.Id, cancellationToken);

        var trainerAccount = trainer is null
            ? null
            : await db.SiteAccounts.SingleOrDefaultAsync(row => row.TrainerId == trainer.Id, cancellationToken);

        var account = memberAccount ?? (trainerAccount is { MemberId: null } ? trainerAccount : null);
        if (account is null)
        {
            account = member is not null ? SiteAccount.ForMember(member.Id, now) : SiteAccount.ForTrainer(trainer!.Id, now);
            db.SiteAccounts.Add(account);
        }
        else
        {
            account.RecordSignIn(now);
        }

        if (member is not null)
        {
            account.LinkMember(member.Id);
        }

        if (trainerAccount is not null && trainerAccount != account)
        {
            trainerAccount.UnlinkTrainer();

            // A trainer-only account left with nothing: its sessions would open no area at all.
            if (trainerAccount.MemberId is null)
            {
                var emptied = trainerAccount.Id;
                await db.RevokeSiteSessionsAsync(
                    db.SiteAccounts.Where(row => row.Id == emptied).Select(row => row.Id),
                    RefreshTokenRevocationReason.PhoneChanged,
                    now,
                    cancellationToken);
            }
        }

        if (trainer is not null)
        {
            account.LinkTrainer(trainer.Id);
        }
        else if (account.TrainerId is not null)
        {
            // The phone is no trainer's any more: the Owner gave the trainer another one.
            account.UnlinkTrainer();
        }

        return account;
    }
}
