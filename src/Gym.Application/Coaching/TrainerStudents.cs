using Gym.Application.Common;
using Gym.Domain.Coaching;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Coaching;

/// <summary>What the Owner stopping a trainer does to their students (BUSINESS_RULES.md §15 <i>Trainers</i>).</summary>
public static class TrainerStudents
{
    /// <summary>
    /// The trainer's links end today, so their students go back to having no trainer and keep
    /// everything recorded about them; the requests waiting for the trainer end too. The caller
    /// holds the coaching lock and saves.
    /// </summary>
    public static async Task ReleaseAsync(IAppDbContext db, Guid trainerId, DateOnly today, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        var links = await db.TrainerLinks
            .Where(link => link.TrainerId == trainerId && link.EndedOn == null)
            .ToListAsync(cancellationToken);
        foreach (var link in links)
        {
            link.End(today, TrainerLinkEndReason.TrainerStopped);
        }

        var waiting = await db.TrainerRequests
            .Where(request => request.TrainerId == trainerId && request.Status == TrainerRequestStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var request in waiting)
        {
            request.EndForStoppedTrainer();
        }
    }
}
