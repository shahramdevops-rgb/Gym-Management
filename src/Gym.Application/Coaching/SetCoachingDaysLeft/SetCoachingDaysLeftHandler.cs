using Gym.Application.Bookings;
using Gym.Application.Common;
using Gym.Domain.Coaching;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Coaching.SetCoachingDaysLeft;

/// <summary>
/// «ویرایش دوره» (15.7k): the trainer corrects the last day of a running coaching period, earlier or
/// later (BUSINESS_RULES.md §15 <i>The coaching period</i>), so a renewal made by mistake can be taken
/// back. The student is not told; the audit log keeps the change.
/// </summary>
/// <remarks>
/// <para>
/// <b>An earlier last day cuts the fixed schedule</b>, as the days left of a link made before 15.7h
/// do: no session is placed after a period. A later one cuts nothing; the trainer saves the program
/// again to use the days, as after «تمدید».
/// </para>
/// <para>
/// <b>A later last day answers the student's «درخواست تمدید»</b> waiting (15.7o); an earlier one
/// leaves it for the trainer to answer.
/// </para>
/// <para>
/// Under the coaching lock, then the trainer's, in the order «تمدید» takes them, so «ذخیره برنامه» reads
/// the period before or after this, never half of it.
/// </para>
/// </remarks>
public sealed class SetCoachingDaysLeftHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<StudentScheduleResponse>> Handle(
        Guid siteAccountId, Guid memberId, SetCoachingDaysLeftCommand command, CancellationToken cancellationToken)
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
        var lastDayBefore = link.PeriodEndsOn;
        var set = link.SetDaysLeft(command.DaysLeft, today);
        if (set.IsFailure)
        {
            return Result.Failure<StudentScheduleResponse>(set.Error);
        }

        if (link.PeriodEndsOn is { } lastDay && lastDay < lastDayBefore)
        {
            await CoachingPeriods.CutAfterAsync(db, link.Id, lastDay, cancellationToken);
        }

        // A later last day answers the student's «درخواست تمدید», as a «تمدید» does; an earlier one
        // leaves it waiting for the trainer (decided with 15.7o's plan).
        if (link.PeriodEndsOn > lastDayBefore)
        {
            await RenewalRequests.AnswerWaitingAsync(db, link.Id, today, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await StudentScheduleResponse.ForAsync(db, link, today, calendar.TimeOfDay(), cancellationToken);
    }
}
