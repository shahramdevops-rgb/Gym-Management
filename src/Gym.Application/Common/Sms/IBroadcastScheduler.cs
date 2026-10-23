namespace Gym.Application.Common.Sms;

/// <summary>
/// Hands a broadcast to the background job runner (BUSINESS_RULES.md §10 <i>The broadcast</i>), so
/// the Owner's page never waits for the provider while hundreds of messages go out.
/// </summary>
/// <remarks>
/// The job runner keeps what it was given in the database, so a broadcast the server stopped in the
/// middle is made again once it is back, and the job then continues from where it was.
/// </remarks>
public interface IBroadcastScheduler
{
    /// <summary>The broadcast is sent as soon as a worker is free.</summary>
    void SendNow(Guid broadcastId);
}
