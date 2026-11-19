using Gym.Application.Bookings;
using Gym.Application.Common;
using Gym.Domain.Coaching;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Coaching.RenewCoachingPeriod;

/// <summary>
/// The trainer renews a student's coaching period, or types the days left of a link made before
/// 15.7h (BUSINESS_RULES.md §15 <i>The coaching period</i>). A renewal starts after the last day, so
/// no day is lost; the days left start today.
/// </summary>
/// <remarks>
/// <para>
/// <b>The days left may cut the fixed schedule.</b> A link made before 15.7h may have a schedule that
/// runs past the days the trainer types: it now ends on the period's last day, one that starts after
/// it is removed, and the dates the student moved to after it go too, since no session is placed
/// after a period. A renewal only adds days, so it cuts nothing.
/// </para>
/// <para>
/// <b>It answers the student's «درخواست تمدید»</b> waiting (15.7o), as «تمدید» from «درخواست‌ها» does.
/// </para>
/// <para>
/// Under the coaching lock, so the link cannot end meanwhile, then the trainer's lock, as a schedule
/// change, so «ذخیره برنامه» reads the period before or after this, never half of it. No handler takes
/// the two the other way round.
/// </para>
/// </remarks>
public sealed class RenewCoachingPeriodHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<StudentScheduleResponse>> Handle(
        Guid siteAccountId, Guid memberId, RenewCoachingPeriodCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockCoachingAsync(cancellationToken);

        var trainer = await CoachingAccess.TrainerAsync(db, siteAccountId, cancellationToken);
        if (trainer.IsFailure)
        {
            return Result.Failure<StudentScheduleResponse>(trainer.Error);
        }

        await db.LockTrainerAsync(trainer.Value.Id, cancellationToken);

        var link = await db.TrainerLinks
            .Include(row => row.Periods)
            .SingleOrDefaultAsync(
                row => row.MemberId == memberId && row.TrainerId == trainer.Value.Id && row.EndedOn == null,
                cancellationToken);
        if (link is null)
        {
            return Result.Failure<StudentScheduleResponse>(CoachingErrors.StudentNotFound);
        }

        var today = calendar.Today();
        var daysLeft = link.Periods.Count == 0;
        var period = link.Renew(command.Days, today);
        if (period.IsFailure)
        {
            return Result.Failure<StudentScheduleResponse>(period.Error);
        }

        if (daysLeft)
        {
            await CoachingPeriods.CutAfterAsync(db, link.Id, period.Value.EndsOn, cancellationToken);
        }

        // A «تمدید» here answers the student's «درخواست تمدید» too (15.7o).
        await RenewalRequests.AnswerWaitingAsync(db, link.Id, today, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await StudentScheduleResponse.ForAsync(db, link, today, calendar.TimeOfDay(), cancellationToken);
    }
}
