using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Application.Members.GetMemberDebt;
using Gym.Domain.Common;

namespace Gym.Application.SiteRecords.GetMyDebt;

/// <summary>The member's own debt, through the panel's breakdown (BUSINESS_RULES.md §15).</summary>
public sealed class GetMyDebtHandler(IAppDbContext db, GetMemberDebtHandler debts)
{
    public async Task<Result<MyDebtResponse>> Handle(Guid siteAccountId, CancellationToken cancellationToken)
    {
        var member = await CoachingAccess.MemberAsync(db, siteAccountId, cancellationToken);
        if (member.IsFailure)
        {
            return Result.Failure<MyDebtResponse>(member.Error);
        }

        var debt = await debts.Handle(member.Value.Id, cancellationToken);

        return debt.IsFailure ? Result.Failure<MyDebtResponse>(debt.Error) : MyDebtResponse.From(debt.Value);
    }
}
