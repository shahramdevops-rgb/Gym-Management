namespace Gym.Application.Common.Sms;

/// <summary>
/// Hands a member's welcome back to the background job runner (BUSINESS_RULES.md §10 <i>The welcome
/// back</i>), half an hour after the check-in, so the desk never waits for the provider.
/// </summary>
/// <remarks>
/// The same as <see cref="IWelcomeScheduler"/>: the job runner keeps what it was given in the
/// database, so a welcome back waiting for its half hour outlives a restart, and the job decides
/// again, when it runs, whether it is still to be sent.
/// </remarks>
public interface IWelcomeBackScheduler
{
    /// <summary>The welcome back for this check-in is sent at this moment (UTC), not before.</summary>
    void SendAt(Guid attendanceId, DateTimeOffset at);
}
