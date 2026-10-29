namespace Gym.Application.Common.Sms;

/// <summary>
/// Hands a trainer's SMS about a new request to the background job runner (BUSINESS_RULES.md §15
/// <i>A member and their trainer</i>, task 15.4c), so the member who picked the trainer never waits
/// for the provider.
/// </summary>
/// <remarks>
/// The same as <see cref="IWelcomeScheduler"/>: kept by the job runner in the database, so an SMS
/// waiting for 10:00 outlives a restart, and the job decides again when it runs.
/// </remarks>
public interface ITrainerRequestSmsScheduler
{
    /// <summary>The SMS for this request is sent as soon as a worker is free.</summary>
    void SendNow(Guid requestId);

    /// <summary>The SMS for this request is sent at this moment (UTC), not before.</summary>
    void SendAt(Guid requestId, DateTimeOffset at);
}
