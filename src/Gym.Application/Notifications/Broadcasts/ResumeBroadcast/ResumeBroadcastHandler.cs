using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Notifications.Broadcasts.ResumeBroadcast;

/// <summary>
/// <c>POST /api/sms/broadcasts/{id}/resume</c>: «ادامه ارسال» for a stopped broadcast
/// (BUSINESS_RULES.md §10 <i>The broadcast</i>, task 10.13). The same text, to the members of the
/// same group as it stands now who have no message of it yet, under the same rules as a new one.
/// Owner only.
/// </summary>
/// <remarks>
/// A group with nobody left in it finishes the broadcast instead: there is nothing more to send.
/// </remarks>
public sealed class ResumeBroadcastHandler(
    IAppDbContext db,
    IGymCalendar calendar,
    TimeProvider time,
    BroadcastRecipients recipients,
    BroadcastSending sending)
{
    public async Task<Result<BroadcastResponse>> Handle(Guid id, CancellationToken cancellationToken)
    {
        var broadcast = await db.SmsBroadcasts.SingleOrDefaultAsync(broadcast => broadcast.Id == id, cancellationToken);
        if (broadcast is null)
        {
            return Result.Failure<BroadcastResponse>(SmsBroadcastErrors.NotFound);
        }

        if (broadcast.Status != BroadcastStatus.Stopped)
        {
            return Result.Failure<BroadcastResponse>(SmsBroadcastErrors.NotStopped);
        }

        if (await sending.CheckCanSendAsync(cancellationToken) is { } notNow)
        {
            return Result.Failure<BroadcastResponse>(notNow);
        }

        var rest = await recipients.FindAsync(broadcast.Audience, calendar.Today(), broadcast.Id, cancellationToken);
        if (rest.Count > 0 && SmsBroadcast.CheckLength(broadcast.Text, BroadcastRecipients.LongestFirstName(rest)) is { } tooLong)
        {
            return Result.Failure<BroadcastResponse>(tooLong);
        }

        if (rest.Count > 0 && await sending.AnySendingAsync(cancellationToken))
        {
            return Result.Failure<BroadcastResponse>(SmsBroadcastErrors.InProgress);
        }

        broadcast.Resume();
        if (rest.Count == 0)
        {
            broadcast.Finish(time.GetUtcNow());
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<BroadcastResponse>(SmsBroadcastErrors.ChangedConcurrently);
        }
        catch (UniqueConstraintException)
        {
            return Result.Failure<BroadcastResponse>(SmsBroadcastErrors.InProgress);
        }

        if (rest.Count > 0)
        {
            await sending.StartAsync(broadcast);
        }

        return BroadcastResponse.From(broadcast, await BroadcastCounts.ForAsync(db, [broadcast.Id], cancellationToken));
    }
}
