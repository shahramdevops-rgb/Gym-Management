using System.Text.Json.Serialization;

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
    /// <param name="today">The gym's today, for whether a renewal may be asked for (15.7o).</param>
    public static async Task<MyTrainerResponse> ForAsync(IAppDbContext db, Guid memberId, DateOnly today, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        var link = await db.TrainerLinks.AsNoTracking()
            .Where(link => link.MemberId == memberId && link.EndedOn == null)
            .Join(db.Trainers, link => link.TrainerId, trainer => trainer.Id,
                (link, trainer) => new
                {
                    LinkId = link.Id,
                    TrainerId = trainer.Id,
                    trainer.FullName,
                    link.StartedOn,
                    PeriodEndsOn = db.CoachingPeriods.Where(period => period.LinkId == link.Id).Max(period => (DateOnly?)period.EndsOn),
                })
            .SingleOrDefaultAsync(cancellationToken);

        MyTrainerLinkResponse? trainer = null;
        if (link is not null)
        {
            // The latest request only: asking again, even taken back, replaces what an earlier one said.
            var latest = await db.RenewalRequests.AsNoTracking()
                .Where(request => request.LinkId == link.LinkId)
                .OrderByDescending(request => request.CreatedAt)
                .ThenByDescending(request => request.Id)
                .FirstOrDefaultAsync(cancellationToken);
            RenewalRequestStatus? renewal = latest switch
            {
                { IsPending: true } => RenewalRequestStatus.Pending,
                not null when latest.ShowsDecline(link.PeriodEndsOn) => RenewalRequestStatus.Declined,
                _ => null,
            };

            var canAsk = renewal != RenewalRequestStatus.Pending
                && RenewalRequest.CanAsk(link.PeriodEndsOn, today, await CoachingPeriods.SoonDaysAsync(db, cancellationToken));

            trainer = new MyTrainerLinkResponse(link.TrainerId, link.FullName, link.StartedOn, link.PeriodEndsOn, renewal, canAsk);
        }

        var request = await db.TrainerRequests.AsNoTracking()
            .Where(request => request.MemberId == memberId && request.Status == TrainerRequestStatus.Pending)
            .Join(db.Trainers, request => request.TrainerId, trainer => trainer.Id,
                (request, trainer) => new MyTrainerRequestResponse(request.Id, trainer.Id, trainer.FullName, request.RequestedOn))
            .SingleOrDefaultAsync(cancellationToken);

        return new MyTrainerResponse(trainer, request);
    }
}

/// <param name="Since">The day the trainer accepted.</param>
/// <param name="PeriodEndsOn">The coaching period's last day (15.7h); <c>null</c> while the trainer has not given one to a link made before it.</param>
/// <param name="Renewal">
/// «درخواست تمدید» (15.7o): <see cref="RenewalRequestStatus.Pending"/> while it waits, or
/// <see cref="RenewalRequestStatus.Declined"/> while the coaching still ends on the day it was asked
/// about; <c>null</c> otherwise.
/// </param>
/// <param name="CanAskRenewal">
/// Whether «درخواست تمدید» shows: the period's last days (the SMS settings' number for them) or after
/// it is over, and no request waiting.
/// </param>
public sealed record MyTrainerLinkResponse(
    Guid TrainerId,
    string FullName,
    DateOnly Since,
    DateOnly? PeriodEndsOn,
    [property: JsonConverter(typeof(JsonStringEnumConverter<RenewalRequestStatus>))] RenewalRequestStatus? Renewal,
    bool CanAskRenewal);

public sealed record MyTrainerRequestResponse(Guid RequestId, Guid TrainerId, string FullName, DateOnly RequestedOn);
