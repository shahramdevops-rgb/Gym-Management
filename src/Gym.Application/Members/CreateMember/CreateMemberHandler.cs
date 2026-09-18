using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Members;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Members.CreateMember;

/// <summary>Registers a member (BUSINESS_RULES.md §2).</summary>
public sealed class CreateMemberHandler(IAppDbContext db, IPhoneNormalizer phones)
{
    public async Task<Result<MemberResponse>> Handle(CreateMemberCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Normalize: every spelling of a number becomes one string before anything compares it.
        var phone = phones.Normalize(command.PhoneNumber);
        if (phone.IsFailure)
        {
            return Result.Failure<MemberResponse>(phone.Error);
        }

        // 2. Check the rule. Inactive members count: a number stays taken after deactivation.
        if (await db.Members.AnyAsync(member => member.PhoneNumber == phone.Value, cancellationToken))
        {
            return Result.Failure<MemberResponse>(MemberErrors.PhoneAlreadyExists);
        }

        // 3. Call the domain.
        var created = Member.Create(command.FullName, phone.Value, command.Notes);
        if (created.IsFailure)
        {
            return Result.Failure<MemberResponse>(created.Error);
        }

        // 4. Save. The check above can pass for two requests at once; the unique index decides.
        db.Members.Add(created.Value);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (exception.ConstraintName == MemberConstraints.UniquePhone)
        {
            return Result.Failure<MemberResponse>(MemberErrors.PhoneAlreadyExists);
        }

        // 5. Map.
        return MemberResponse.From(created.Value);
    }
}
