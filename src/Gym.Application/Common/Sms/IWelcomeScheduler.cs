namespace Gym.Application.Common.Sms;

/// <summary>
/// Hands a member's welcome to the background job runner (BUSINESS_RULES.md §10 <i>The welcome</i>),
/// so the desk that sold the first plan never waits for the provider.
/// </summary>
/// <remarks>
/// The same as <see cref="ISubscriptionReceiptScheduler"/>: the job runner keeps what it was given in
/// the database, so a welcome waiting for 10:00 outlives a restart, and the job decides again, when it
/// runs, whether the welcome is still to be sent.
/// </remarks>
public interface IWelcomeScheduler
{
    /// <summary>The welcome for this first plan is sent as soon as a worker is free.</summary>
    void SendNow(Guid subscriptionId);

    /// <summary>The welcome for this first plan is sent at this moment (UTC), not before.</summary>
    void SendAt(Guid subscriptionId, DateTimeOffset at);
}
