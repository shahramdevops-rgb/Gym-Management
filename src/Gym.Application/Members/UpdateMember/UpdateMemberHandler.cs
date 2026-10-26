using Gym.Application.Common;
using Gym.Domain.Auth;
using Gym.Domain.Common;
using Gym.Domain.Members;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Members.UpdateMember;

/// <summary>
/// Replaces a member's details. Works for inactive members too (BUSINESS_RULES.md §2).
/// </summary>
/// <remarks>
/// Two layers stop one person's edit from silently erasing another's:
/// <list type="bullet">
/// <item>The <c>Version</c> the client read. If the member was saved since, the edit was made on
/// stale data, and it is refused before anything changes.</item>
/// <item><c>xmin</c> on save. It covers the moment between this request's read and its write,
/// which the version check cannot.</item>
/// </list>
/// <para>
/// <b>A new phone signs the member out of the site</b> (BUSINESS_RULES.md §15). The site finds a
/// member by the phone the desk typed; if that was a stranger's number, the stranger may have signed
/// in as this member. Correcting it ends every site session of the member in the same save, and the
/// member signs in again with the right number.
/// </para>
/// </remarks>
public sealed class UpdateMemberHandler(IAppDbContext db, IPhoneNormalizer phones, IGymCalendar calendar, TimeProvider timeProvider)
{
    public async Task<Result<MemberResponse>> Handle(Guid id, UpdateMemberCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var member = await db.Members.SingleOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (member is null)
        {
            return Result.Failure<MemberResponse>(MemberErrors.NotFound);
        }

        if (member.Version != command.Version)
        {
            return Result.Failure<MemberResponse>(MemberErrors.ChangedConcurrently);
        }

        var phone = phones.Normalize(command.PhoneNumber);
        if (phone.IsFailure)
        {
            return Result.Failure<MemberResponse>(phone.Error);
        }

        // Keeping one's own number is fine; taking another member's is not.
        if (await db.Members.AnyAsync(m => m.PhoneNumber == phone.Value && m.Id != id, cancellationToken))
        {
            return Result.Failure<MemberResponse>(MemberErrors.PhoneAlreadyExists);
        }

        // The validator already refused a missing date; this only turns DateOnly? into DateOnly.
        if (command.BirthDate is not { } birthDate)
        {
            return Result.Failure<MemberResponse>(MemberErrors.BirthDateRequired);
        }

        var phoneChanged = member.PhoneNumber != phone.Value;

        var updated = member.Update(command.FullName, phone.Value, command.Notes, birthDate, calendar.Today());
        if (updated.IsFailure)
        {
            return Result.Failure<MemberResponse>(updated.Error);
        }

        if (phoneChanged)
        {
            await SignOutOfTheSiteAsync(id, cancellationToken);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (exception.ConstraintName == MemberConstraints.UniquePhone)
        {
            return Result.Failure<MemberResponse>(MemberErrors.PhoneAlreadyExists);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<MemberResponse>(MemberErrors.ChangedConcurrently);
        }

        return MemberResponse.From(member);
    }

    /// <summary>Revokes the member's live site sessions; saved with the member's change.</summary>
    private async Task SignOutOfTheSiteAsync(Guid memberId, CancellationToken cancellationToken)
    {
        var accountIds = db.SiteAccounts.Where(account => account.MemberId == memberId).Select(account => account.Id);
        var sessions = await db.SiteRefreshTokens
            .Where(token => token.RevokedAt == null && accountIds.Contains(token.SiteAccountId))
            .ToListAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        foreach (var session in sessions)
        {
            session.Revoke(RefreshTokenRevocationReason.PhoneChanged, now);
        }
    }
}
