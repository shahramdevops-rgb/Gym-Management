using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Notifications;

namespace Gym.Application.Notifications.Broadcasts.SendBroadcast;

/// <summary>
/// <c>POST /api/sms/broadcasts</c>: the Owner sends a broadcast (BUSINESS_RULES.md §10 <i>The
/// broadcast</i>, task 10.13). Owner only.
/// </summary>
/// <remarks>
/// <para>
/// <b>Saved here, sent by the job.</b> The broadcast is saved <c>Sending</c> and handed to the job
/// runner after the save, so the page answers at once and the job finds the row. The messages
/// themselves are written by the job, one at a time, each just before its request
/// (<c>RunBroadcastHandler</c>).
/// </para>
/// <para>
/// <b>Checked in the order the Owner can do something about</b>: the text, then the switches and the
/// hours, then the group, then whether another broadcast is still going.
/// </para>
/// </remarks>
public sealed class SendBroadcastHandler(
    IAppDbContext db,
    IGymCalendar calendar,
    BroadcastRecipients recipients,
    BroadcastSending sending)
{
    public async Task<Result<BroadcastResponse>> Handle(SendBroadcastCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var created = SmsBroadcast.Create(command.Text ?? string.Empty, command.Audience);
        if (created.IsFailure)
        {
            return Result.Failure<BroadcastResponse>(created.Error);
        }

        var broadcast = created.Value;

        if (await sending.CheckCanSendAsync(cancellationToken) is { } notNow)
        {
            return Result.Failure<BroadcastResponse>(notNow);
        }

        var group = await recipients.FindAsync(broadcast.Audience, calendar.Today(), withoutMessageOf: null, cancellationToken);
        if (group.Count == 0)
        {
            return Result.Failure<BroadcastResponse>(SmsBroadcastErrors.NoRecipients);
        }

        if (SmsBroadcast.CheckLength(broadcast.Text, BroadcastRecipients.LongestFirstName(group)) is { } tooLong)
        {
            return Result.Failure<BroadcastResponse>(tooLong);
        }

        if (await sending.AnySendingAsync(cancellationToken))
        {
            return Result.Failure<BroadcastResponse>(SmsBroadcastErrors.InProgress);
        }

        db.SmsBroadcasts.Add(broadcast);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            // Another one was saved between the check and here: the index holds one at a time.
            return Result.Failure<BroadcastResponse>(SmsBroadcastErrors.InProgress);
        }

        await sending.StartAsync(broadcast);

        return BroadcastResponse.From(broadcast, []);
    }
}
