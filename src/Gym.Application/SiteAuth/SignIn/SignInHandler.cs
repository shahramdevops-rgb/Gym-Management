using Gym.Application.Common;
using Gym.Application.Common.Security;
using Gym.Application.SiteAuth.VerifyCode;
using Gym.Domain.Common;
using Gym.Domain.SiteAuth;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.SiteAuth.SignIn;

/// <summary>
/// The code typed on the site's sign-in screen (BUSINESS_RULES.md §15 <i>Signing up and signing
/// in</i>). A member's phone signs in, and the first time links an account to that member; a phone
/// the gym does not know gets a ticket for the sign-up screen.
/// </summary>
/// <remarks>
/// <para>
/// <b>The member is found by the phone, the account by the member.</b> The panel's phone is the
/// only one there is (see <c>SiteAccount</c>), so a number the desk changed signs in as the member
/// it now belongs to, and the old number no longer reaches them.
/// </para>
/// <para>
/// <b>Two steps, two transactions.</b> The code is checked first and its try or its use is saved
/// whatever comes next (<see cref="VerifyCodeHandler"/>). Then the phone is locked again while the
/// account is found or made, so a sign-up for the same phone at the same moment cannot make a
/// second one. The unique index on the account's member is the database's own guard behind that.
/// </para>
/// </remarks>
public sealed class SignInHandler(
    IAppDbContext db,
    VerifyCodeHandler verifyCode,
    SiteSessions sessions,
    ISiteTokens tokens,
    TimeProvider timeProvider)
{
    public async Task<Result<SignInResult>> Handle(SignInCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var verified = await verifyCode.Handle(new VerifyCodeCommand(command.PhoneNumber, command.Code), cancellationToken);
        if (verified.IsFailure)
        {
            return Result.Failure<SignInResult>(verified.Error);
        }

        var phoneNumber = verified.Value;

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockPhoneAsync(phoneNumber, cancellationToken);

        var member = await db.Members.SingleOrDefaultAsync(row => row.PhoneNumber == phoneNumber, cancellationToken);
        if (member is null)
        {
            var ticket = tokens.IssueSignUpTicket(phoneNumber);

            return new SignInResult(new SignInResponse(SignInOutcome.NeedsProfile, null, ticket.Value, ticket.ExpiresAt), null);
        }

        var now = timeProvider.GetUtcNow();
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

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (exception.ConstraintName == SiteAuthConstraints.UniqueMember)
        {
            return Result.Failure<SignInResult>(SiteAuthErrors.SignInConflict);
        }

        await transaction.CommitAsync(cancellationToken);

        return new SignInResult(new SignInResponse(SignInOutcome.SignedIn, session.Response, null, null), session);
    }
}
