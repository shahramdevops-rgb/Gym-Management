namespace Gym.Application.Common;

/// <summary>Business limits for attendance (BUSINESS_RULES.md §7 Cancel check-in, Auto-checkout).</summary>
public interface IAttendancePolicy
{
    /// <summary><c>Gym:CancelCheckInWindowMinutes</c>.</summary>
    int CancelWindowMinutes { get; }

    /// <summary>
    /// <c>Gym:ClosingTime</c>: the local time the nightly auto-checkout job runs at. Only the
    /// job's own schedule (Gym.Infrastructure/Jobs) reads this; the job itself just closes
    /// whatever is open when it runs, the same way <see cref="CancelWindowMinutes"/> is read by
    /// the handler and not carried by the entity.
    /// </summary>
    TimeOnly ClosingTime { get; }
}
