namespace Gym.Application.Common.Sms;

/// <summary>
/// Hands a student's SMS about a cancelled session to the background job runner (BUSINESS_RULES.md
/// §15 <i>Timetable and attendance</i>, task 15.6c), so the trainer who saved the change never waits
/// for the provider.
/// </summary>
/// <remarks>
/// The same as <see cref="ITrainerRequestSmsScheduler"/>: kept by the job runner in the database, so an
/// SMS waiting for 08:00 outlives a restart, and the job decides again when it runs.
/// </remarks>
public interface ISessionCancelledSmsScheduler
{
    /// <summary>The SMS for this student and change is sent as soon as a worker is free.</summary>
    void SendNow(Guid changeId, Guid memberId);

    /// <summary>The SMS for this student and change is sent at this moment (UTC), not before.</summary>
    void SendAt(Guid changeId, Guid memberId, DateTimeOffset at);
}
