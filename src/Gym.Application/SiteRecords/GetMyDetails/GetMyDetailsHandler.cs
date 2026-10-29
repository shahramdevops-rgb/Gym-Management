using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Application.Members.GetMember;
using Gym.Domain.Common;

namespace Gym.Application.SiteRecords.GetMyDetails;

/// <summary>The member's own details, through the panel's profile handler (BUSINESS_RULES.md §15).</summary>
public sealed class GetMyDetailsHandler(IAppDbContext db, GetMemberHandler members)
{
    public async Task<Result<MyDetailsResponse>> Handle(Guid siteAccountId, CancellationToken cancellationToken)
    {
        var member = await CoachingAccess.MemberAsync(db, siteAccountId, cancellationToken);
        if (member.IsFailure)
        {
            return Result.Failure<MyDetailsResponse>(member.Error);
        }

        var profile = await members.Handle(member.Value.Id, cancellationToken);
        if (profile.IsFailure)
        {
            return Result.Failure<MyDetailsResponse>(profile.Error);
        }

        var details = profile.Value;

        // Signed in, so the account is there: it is the one the token was made for.
        return new MyDetailsResponse(
            details.FullName,
            details.PhoneNumber,
            details.BirthDate,
            details.CreatedAt,
            details.SiteAccount!.CreatedAt);
    }
}
