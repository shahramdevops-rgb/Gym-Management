using Gym.Application.Common;
using Gym.Domain.Common;

namespace Gym.Application.Coaching.GetMyTrainer;

/// <summary>«مربی من»: the member's trainer now and their request waiting for an answer (BUSINESS_RULES.md §15).</summary>
public sealed class GetMyTrainerHandler(IAppDbContext db)
{
    public async Task<Result<MyTrainerResponse>> Handle(Guid siteAccountId, CancellationToken cancellationToken)
    {
        var member = await CoachingAccess.MemberAsync(db, siteAccountId, cancellationToken);
        if (member.IsFailure)
        {
            return Result.Failure<MyTrainerResponse>(member.Error);
        }

        return await MyTrainerResponse.ForAsync(db, member.Value.Id, cancellationToken);
    }
}
