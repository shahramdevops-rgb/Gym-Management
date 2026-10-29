using Gym.Application.Common;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Coaching.ListTrainerChoices;

/// <summary>
/// The trainers a member can pick on the site (BUSINESS_RULES.md §15): trainers now, by name only
/// until the gym's photos and details come (14.1). A trainer who is this member, with the same
/// phone, is left out: nobody is their own trainer.
/// </summary>
public sealed class ListTrainerChoicesHandler(IAppDbContext db)
{
    public async Task<Result<IReadOnlyList<TrainerChoiceResponse>>> Handle(Guid siteAccountId, CancellationToken cancellationToken)
    {
        var member = await CoachingAccess.MemberAsync(db, siteAccountId, cancellationToken);
        if (member.IsFailure)
        {
            return Result.Failure<IReadOnlyList<TrainerChoiceResponse>>(member.Error);
        }

        var phone = member.Value.PhoneNumber;
        var trainers = await db.Trainers.AsNoTracking()
            .Where(trainer => trainer.Periods.Any(period => period.EndedOn == null) && trainer.PhoneNumber != phone)
            .OrderBy(trainer => trainer.FullName)
            .Select(trainer => new TrainerChoiceResponse(trainer.Id, trainer.FullName))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<TrainerChoiceResponse>>(trainers);
    }
}
