using Gym.Application.Common;
using Gym.Domain.Bookings;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Bookings;

/// <summary>
/// «درخواست‌های مرخصی» on the trainer's home (BUSINESS_RULES.md §15 <i>Leave by request</i>, task
/// 15.7i): the requests waiting for their answer, the nearest session first.
/// </summary>
/// <remarks>
/// Only a request still worth an answer: its session has not started (by then it is declined), and
/// the student is still in it, since a date moved, taken out or cancelled meanwhile has nothing to
/// leave.
/// </remarks>
public sealed record LeaveRequestsResponse(IReadOnlyList<LeaveRequestItem> Requests)
{
    internal static async Task<LeaveRequestsResponse> ForAsync(
        IAppDbContext db, Guid trainerId, DateOnly today, TimeOnly timeOfDay, CancellationToken cancellationToken)
    {
        var pending = await db.LeaveRequests.AsNoTracking()
            .Where(request => request.TrainerId == trainerId && request.Status == LeaveRequestStatus.Pending && request.Date >= today)
            .Join(db.Members, request => request.MemberId, member => member.Id, (request, member) => new { Request = request, Member = member })
            .ToListAsync(cancellationToken);

        var waiting = pending.Where(row => row.Request.IsWaiting(today, timeOfDay)).ToList();
        if (waiting.Count == 0)
        {
            return new LeaveRequestsResponse([]);
        }

        var places = await BookedPlaces.OfTrainerAsync(
            db, trainerId, waiting.Min(row => row.Request.Date), waiting.Max(row => row.Request.Date), cancellationToken);
        var held = places.Select(place => (place.MemberId, place.Date, place.Start, place.End)).ToHashSet();

        var items = waiting
            .Where(row => held.Contains((row.Request.MemberId, row.Request.Date, row.Request.Start, row.Request.End)))
            .OrderBy(row => row.Request.Date).ThenBy(row => row.Request.Start).ThenBy(row => row.Member.FullName)
            .Select(row => new LeaveRequestItem(
                row.Request.Id,
                row.Member.Id,
                row.Member.FullName,
                row.Member.PhoneNumber,
                row.Request.Date,
                row.Request.Start,
                row.Request.End,
                row.Request.RequestedOn))
            .ToList();

        return new LeaveRequestsResponse(items);
    }
}

/// <param name="RequestedOn">The gym's day the student asked.</param>
public sealed record LeaveRequestItem(
    Guid RequestId,
    Guid MemberId,
    string FullName,
    string PhoneNumber,
    DateOnly Date,
    TimeOnly Start,
    TimeOnly End,
    DateOnly RequestedOn);
