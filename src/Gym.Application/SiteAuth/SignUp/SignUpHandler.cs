using Gym.Application.Common;
using Gym.Application.Common.Security;
using Gym.Application.Members;
using Gym.Domain.Common;
using Gym.Domain.Members;
using Gym.Domain.SiteAuth;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.SiteAuth.SignUp;

/// <summary>
/// A new phone gives its name and birth date (BUSINESS_RULES.md §15): the member, the account and
/// the session are made in one transaction. The member is active and has no plan, and no welcome
/// SMS goes out; that waits for the first plan, at the desk (§10 <i>The welcome</i>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The ticket is the proof of the phone.</b> It was signed by this API when the code was right,
/// and lives 15 minutes; nothing typed here can name another phone.
/// </para>
/// <para>
/// <b>If the phone became a member meanwhile</b> (the desk registered them, or the same ticket was
/// sent twice), that member is signed in as they are, and the name and date typed here are not
/// used: a sign-up never changes what the desk entered (§15).
/// </para>
/// </remarks>
public sealed class SignUpHandler(
    IAppDbContext db,
    ISiteTokens tokens,
    SiteSessions sessions,
    IGymCalendar calendar,
    TimeProvider timeProvider)
{
    public async Task<Result<SiteSession>> Handle(SignUpCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await tokens.ReadSignUpTicketAsync(command.Ticket) is not { } phoneNumber)
        {
            return Result.Failure<SiteSession>(SiteAuthErrors.SignUpExpired);
        }

        // The validator already refused a missing date; this only turns DateOnly? into DateOnly.
        if (command.BirthDate is not { } birthDate)
        {
            return Result.Failure<SiteSession>(MemberErrors.BirthDateRequired);
        }

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockPhoneAsync(phoneNumber, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var member = await db.Members.SingleOrDefaultAsync(row => row.PhoneNumber == phoneNumber, cancellationToken);
        if (member is null)
        {
            var created = Member.Create(command.FullName, phoneNumber, notes: null, birthDate, calendar.DayOf(now));
            if (created.IsFailure)
            {
                return Result.Failure<SiteSession>(created.Error);
            }

            member = created.Value;
            db.Members.Add(member);
        }

        var account = await db.SiteAccounts.SingleOrDefaultAsync(row => row.MemberId == member.Id, cancellationToken);
        if (account is null)
        {
            account = SiteAccount.ForMember(member.Id, now);
            db.SiteAccounts.Add(account);
        }
        else
        {
            account.RecordSignIn(now);
        }

        var session = sessions.Start(account, member, now);

        // A member the desk saves with this phone at this very moment is the one race the phone's
        // lock cannot hold off (the desk does not take it): the unique index refuses the second.
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (exception.ConstraintName == MemberConstraints.UniquePhone)
        {
            return Result.Failure<SiteSession>(MemberErrors.PhoneAlreadyExists);
        }
        catch (UniqueConstraintException exception) when (exception.ConstraintName == SiteAuthConstraints.UniqueMember)
        {
            return Result.Failure<SiteSession>(SiteAuthErrors.SignInConflict);
        }

        await transaction.CommitAsync(cancellationToken);

        return session;
    }
}
