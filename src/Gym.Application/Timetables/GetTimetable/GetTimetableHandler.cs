using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Domain.Common;

namespace Gym.Application.Timetables.GetTimetable;

/// <summary>The trainer opens «تمام سانس‌های من» (BUSINESS_RULES.md §15 <i>The trainer's sessions</i>).</summary>
public sealed class GetTimetableHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<TimetableResponse>> Handle(Guid siteAccountId, CancellationToken cancellationToken)
    {
        var trainer = await CoachingAccess.TrainerAsync(db, siteAccountId, cancellationToken);
        if (trainer.IsFailure)
        {
            return Result.Failure<TimetableResponse>(trainer.Error);
        }

        return await TimetableResponse.ForAsync(db, trainer.Value.Id, calendar.Today(), cancellationToken);
    }
}
