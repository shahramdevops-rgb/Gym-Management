using Gym.Application.Common;
using Gym.Domain.Common;

namespace Gym.Application.Coaching.GetStudentNews;

/// <summary>«خبر شاگردان» on the trainer's home (BUSINESS_RULES.md §15, task 15.4d).</summary>
public sealed class GetStudentNewsHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<CoachingNewsResponse>> Handle(Guid siteAccountId, CancellationToken cancellationToken)
    {
        var trainer = await CoachingAccess.TrainerAsync(db, siteAccountId, cancellationToken);
        if (trainer.IsFailure)
        {
            return Result.Failure<CoachingNewsResponse>(trainer.Error);
        }

        return await CoachingNewsResponse.ForTrainerAsync(db, trainer.Value.Id, calendar.Today(), cancellationToken);
    }
}
