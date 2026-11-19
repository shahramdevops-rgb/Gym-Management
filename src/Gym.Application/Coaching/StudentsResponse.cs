using Gym.Application.Common;
using Gym.Domain.Coaching;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Coaching;

/// <summary>
/// The trainer's side of the site (BUSINESS_RULES.md §15 <i>A member and their trainer</i>): the
/// requests waiting for their answer, and «شاگردان من». The trainer sees each member's name and
/// phone (decided by the developer, 1405/08/07, 15.4).
/// </summary>
/// <remarks>
/// A request from a member the desk has deactivated is left out until the member is active again:
/// it cannot be accepted meanwhile. A student the desk deactivated stays, marked
/// <see cref="StudentResponse.IsActive"/> false, since the link outlives it.
/// </remarks>
/// <param name="PeriodSoonDays">
/// From this many days left «شاگردان من» marks a student, the last day counting as one: the SMS
/// settings' number for the coaching period's last days (§15 <i>The coaching period</i>, task 15.7n),
/// whether that SMS is on or off, so the trainer sees it the day the student gets the SMS.
/// </param>
public sealed record StudentsResponse(
    IReadOnlyList<StudentRequestResponse> Requests, IReadOnlyList<StudentResponse> Students, int PeriodSoonDays)
{
    public static async Task<StudentsResponse> ForAsync(IAppDbContext db, Guid trainerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        // Ordered before the records are made: EF Core cannot order by a constructor's argument.
        var requests = await db.TrainerRequests.AsNoTracking()
            .Where(request => request.TrainerId == trainerId && request.Status == TrainerRequestStatus.Pending)
            .Join(db.Members.Where(member => member.IsActive), request => request.MemberId, member => member.Id,
                (request, member) => new { Request = request, Member = member })
            .OrderBy(pair => pair.Request.CreatedAt)
            .Select(pair => new StudentRequestResponse(
                pair.Request.Id, pair.Member.Id, pair.Member.FullName, pair.Member.PhoneNumber, pair.Request.RequestedOn))
            .ToListAsync(cancellationToken);

        var students = await db.TrainerLinks.AsNoTracking()
            .Where(link => link.TrainerId == trainerId && link.EndedOn == null)
            .Join(db.Members, link => link.MemberId, member => member.Id, (link, member) => new { Link = link, Member = member })
            .OrderBy(pair => pair.Member.FullName)
            .Select(pair => new StudentResponse(
                pair.Member.Id,
                pair.Member.FullName,
                pair.Member.PhoneNumber,
                pair.Link.StartedOn,
                pair.Member.IsActive,
                db.CoachingPeriods.Where(period => period.LinkId == pair.Link.Id).Max(period => (DateOnly?)period.EndsOn)))
            .ToListAsync(cancellationToken);

        var periodSoonDays = await db.SmsSettings.AsNoTracking()
            .Where(settings => settings.Id == SmsSettings.TheId)
            .Select(settings => settings.CoachingPeriodEndingDays)
            .SingleAsync(cancellationToken);

        return new StudentsResponse(requests, students, periodSoonDays);
    }
}

public sealed record StudentRequestResponse(Guid RequestId, Guid MemberId, string FullName, string PhoneNumber, DateOnly RequestedOn);

/// <param name="Since">The day the trainer accepted.</param>
/// <param name="IsActive">False once the desk has deactivated the member.</param>
/// <param name="PeriodEndsOn">The coaching period's last day (15.7h); <c>null</c> for a link made before it whose days left are not typed yet.</param>
public sealed record StudentResponse(Guid MemberId, string FullName, string PhoneNumber, DateOnly Since, bool IsActive, DateOnly? PeriodEndsOn);
