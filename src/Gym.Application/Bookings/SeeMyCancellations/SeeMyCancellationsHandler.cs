using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Bookings.SeeMyCancellations;

/// <summary>
/// The member presses «دیدم» on «سانس لغو شد» (BUSINESS_RULES.md §15 <i>Timetable and attendance</i>,
/// task 15.6c): every cancellation of theirs saved up to what they read is seen, with any trainer, so
/// it leaves the home and its SMS, if still waiting, is not sent. Pressed twice, nothing changes.
/// </summary>
public sealed class SeeMyCancellationsHandler(IAppDbContext db, IGymCalendar calendar, TimeProvider time)
{
    public async Task<Result<MyCancellationsResponse>> Handle(Guid siteAccountId, SeeMyCancellationsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var member = await CoachingAccess.MemberAsync(db, siteAccountId, cancellationToken);
        if (member.IsFailure)
        {
            return Result.Failure<MyCancellationsResponse>(member.Error);
        }

        var memberId = member.Value.Id;
        var unseen = await db.SessionCancellations
            .Where(row => row.MemberId == memberId && row.SeenAt == null && row.CreatedAt <= command.UpTo)
            .ToListAsync(cancellationToken);

        var now = time.GetUtcNow();
        foreach (var row in unseen)
        {
            row.MarkSeen(now);
        }

        await db.SaveChangesAsync(cancellationToken);

        return await MyCancellationsResponse.ForAsync(db, memberId, calendar.Today(), cancellationToken);
    }
}
