using Gym.Application.Common;
using Gym.Domain.Coaching;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Coaching;

/// <summary>
/// What the trainer's other actions do to a student's «درخواست تمدید» (BUSINESS_RULES.md §15 <i>The
/// coaching period</i>, task 15.7o). The callers hold the coaching lock and save.
/// </summary>
internal static class RenewalRequests
{
    /// <summary>The link's request waiting for the trainer, tracked; <c>null</c> when none.</summary>
    public static Task<RenewalRequest?> WaitingAsync(IAppDbContext db, Guid linkId, CancellationToken cancellationToken) =>
        db.RenewalRequests.SingleOrDefaultAsync(
            request => request.LinkId == linkId && request.Status == RenewalRequestStatus.Pending,
            cancellationToken);

    /// <summary>
    /// The trainer's student's request with that id, tracked, whatever became of it; <c>null</c> when
    /// it is another trainer's, or its member was deactivated at the desk since asking: it is left out
    /// of «درخواست‌ها» and waits until the member is active again (decided with 15.7o's plan).
    /// </summary>
    public static Task<RenewalRequest?> OfTrainerAsync(IAppDbContext db, Guid trainerId, Guid requestId, CancellationToken cancellationToken) =>
        db.RenewalRequests.SingleOrDefaultAsync(
            request => request.Id == requestId
                && db.TrainerLinks.Any(link => link.Id == request.LinkId
                    && link.TrainerId == trainerId
                    && db.Members.Any(member => member.Id == link.MemberId && member.IsActive)),
            cancellationToken);

    /// <summary>
    /// A renewal the trainer made elsewhere answers the waiting request: a «تمدید» on «برنامهٔ ثابت»,
    /// or a «ویرایش دوره» that moved the last day later.
    /// </summary>
    public static async Task AnswerWaitingAsync(IAppDbContext db, Guid linkId, DateOnly today, CancellationToken cancellationToken)
    {
        var waiting = await WaitingAsync(db, linkId, cancellationToken);
        waiting?.Renew(today);
    }

    /// <summary>Ending the coaching closes the waiting request, however it ended.</summary>
    public static async Task CloseForEndedLinksAsync(IAppDbContext db, IReadOnlyCollection<Guid> linkIds, CancellationToken cancellationToken)
    {
        if (linkIds.Count == 0)
        {
            return;
        }

        var waiting = await db.RenewalRequests
            .Where(request => linkIds.Contains(request.LinkId) && request.Status == RenewalRequestStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var request in waiting)
        {
            request.CloseForEndedLink();
        }
    }
}
