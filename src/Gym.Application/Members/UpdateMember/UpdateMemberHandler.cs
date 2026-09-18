using Gym.Application.Common;
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
/// </remarks>
public sealed class UpdateMemberHandler(IAppDbContext db, IPhoneNormalizer phones)
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

        var updated = member.Update(command.FullName, phone.Value, command.Notes);
        if (updated.IsFailure)
        {
            return Result.Failure<MemberResponse>(updated.Error);
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
}
