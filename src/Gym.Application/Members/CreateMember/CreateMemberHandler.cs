using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Members;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Members.CreateMember;

/// <summary>Registers a member (BUSINESS_RULES.md §2).</summary>
public sealed class CreateMemberHandler(IAppDbContext db, IPhoneNormalizer phones, IGymCalendar calendar)
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

        // 2. Check the rule. Inactive members count: a number stays taken after deactivation. A
        // site user's number is taken too, but the form brings them in instead (§16), so it is
        // told apart.
        var withPhone = db.Members.Where(member => member.PhoneNumber == phone.Value);
        if (await withPhone.AnyAsync(cancellationToken))
        {
            var isSiteUser = await withPhone.AnyAsync(SiteUserRule.IsSiteUser(db), cancellationToken);

            return Result.Failure<MemberResponse>(
                isSiteUser ? MemberErrors.PhoneBelongsToSiteUser : MemberErrors.PhoneAlreadyExists);
        }

        // 3. Call the domain. The birth date rules need a today, and the gym's today is the one
        // in its own time zone, so it is passed in rather than read from a clock in the entity.
        // The validator already refused a missing date; this only turns DateOnly? into DateOnly.
        if (command.BirthDate is not { } birthDate)
        {
            return Result.Failure<MemberResponse>(MemberErrors.BirthDateRequired);
        }

        var created = Member.Create(command.FullName, phone.Value, command.Notes, birthDate, calendar.Today());
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
            // The same number saved meanwhile, by the desk or by a sign-up on the site: told apart
            // as in step 2, so the form can still offer «ثبت به‌عنوان عضو».
            db.Members.Remove(created.Value);
            var isSiteUser = await db.Members
                .Where(member => member.PhoneNumber == phone.Value)
                .AnyAsync(SiteUserRule.IsSiteUser(db), cancellationToken);

            return Result.Failure<MemberResponse>(
                isSiteUser ? MemberErrors.PhoneBelongsToSiteUser : MemberErrors.PhoneAlreadyExists);
        }

        // 5. Map.
        return MemberResponse.From(created.Value);
    }
}
