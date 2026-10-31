using Gym.Application.Bookings.SetStudentSchedule;
using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Domain.Common;

namespace Gym.Application.Bookings.CheckStudentSchedule;

/// <summary>
/// What «ذخیره برنامه» would do, before the trainer presses it (BUSINESS_RULES.md §15 <i>Students in
/// sessions</i>): how many dates the schedule puts the student in, and which of them have no room.
/// Nothing is kept.
/// </summary>
/// <remarks>
/// The schedule is saved as <see cref="SetStudentScheduleHandler"/> saves it, counted, then rolled
/// back with the transaction: the count is the save's own, not a second copy of its rules.
/// </remarks>
public sealed class CheckStudentScheduleHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<ScheduleCheckResponse>> Handle(
        Guid siteAccountId, Guid memberId, SetStudentScheduleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var trainer = await CoachingAccess.TrainerAsync(db, siteAccountId, cancellationToken);
        if (trainer.IsFailure)
        {
            return Result.Failure<ScheduleCheckResponse>(trainer.Error);
        }

        await db.LockTrainerAsync(trainer.Value.Id, cancellationToken);

        var applied = await FixedScheduleChange.ApplyAsync(db, trainer.Value.Id, memberId, command, calendar.Today(), cancellationToken);
        if (applied.IsFailure)
        {
            return Result.Failure<ScheduleCheckResponse>(applied.Error);
        }

        var fit = applied.Value.Fit;

        return new ScheduleCheckResponse(
            fit.Held.Count,
            fit.Full.Select(place => new SessionDateResponse(place.Date, place.Start, place.End)).ToList());
    }
}
