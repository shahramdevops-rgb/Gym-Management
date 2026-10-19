using Gym.Domain.Notifications;

namespace Gym.Application.Notifications.Broadcasts.RunBroadcast;

/// <summary>What one run of a broadcast's job did, for the log and the tests.</summary>
/// <param name="Ran"><c>false</c> when the broadcast was not found or was no longer sending.</param>
/// <param name="Interrupted">Its messages a stopped job left <c>Pending</c>, now <c>Unknown</c>.</param>
public sealed record BroadcastRunResult(
    bool Ran,
    BroadcastStatus? Status = null,
    int Sent = 0,
    int Failed = 0,
    int Unknown = 0,
    int Interrupted = 0)
{
    public static readonly BroadcastRunResult NotRun = new(Ran: false);
}
