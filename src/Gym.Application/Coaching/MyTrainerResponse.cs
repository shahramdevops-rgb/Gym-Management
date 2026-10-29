using Gym.Application.Common;
using Gym.Domain.Coaching;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Coaching;

/// <summary>
/// «مربی من» on the member's side of the site (BUSINESS_RULES.md §15 <i>A member and their
/// trainer</i>): the trainer now, the request waiting for an answer, either, both or neither. Both
/// at once is a member changing trainer: the old one stays until the new one accepts.
/// </summary>
public sealed record MyTrainerResponse(MyTrainerLinkResponse? Trainer, MyTrainerRequestResponse? Request)
{
    public static async Task<MyTrainerResponse> ForAsync(IAppDbContext db, Guid memberId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        var trainer = await db.TrainerLinks.AsNoTracking()
            .Where(link => link.MemberId == memberId && link.EndedOn == null)
            .Join(db.Trainers, link => link.TrainerId, trainer => trainer.Id,
                (link, trainer) => new MyTrainerLinkResponse(trainer.Id, trainer.FullName, link.StartedOn))
            .SingleOrDefaultAsync(cancellationToken);

        var request = await db.TrainerRequests.AsNoTracking()
            .Where(request => request.MemberId == memberId && request.Status == TrainerRequestStatus.Pending)
            .Join(db.Trainers, request => request.TrainerId, trainer => trainer.Id,
                (request, trainer) => new MyTrainerRequestResponse(request.Id, trainer.Id, trainer.FullName, request.RequestedOn))
            .SingleOrDefaultAsync(cancellationToken);

        return new MyTrainerResponse(trainer, request);
    }
}

/// <param name="Since">The day the trainer accepted.</param>
public sealed record MyTrainerLinkResponse(Guid TrainerId, string FullName, DateOnly Since);

public sealed record MyTrainerRequestResponse(Guid RequestId, Guid TrainerId, string FullName, DateOnly RequestedOn);
