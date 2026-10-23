namespace Gym.Application.Common.Sms;

/// <summary>
/// Hands a subscription's receipt to the background job runner (BUSINESS_RULES.md §10 <i>The
/// subscription receipt</i>), so the desk that took the payment never waits for the provider.
/// </summary>
/// <remarks>
/// The job runner (Hangfire, in Gym.Infrastructure/Jobs) keeps what it was given in the database, so
/// a receipt waiting for 10:00 outlives a restart. The job decides again, when it runs, whether the
/// receipt is still to be sent.
/// </remarks>
public interface ISubscriptionReceiptScheduler
{
    /// <summary>The receipt is sent as soon as a worker is free.</summary>
    void SendNow(Guid subscriptionId);

    /// <summary>The receipt is sent at this moment (UTC), not before.</summary>
    void SendAt(Guid subscriptionId, DateTimeOffset at);
}
