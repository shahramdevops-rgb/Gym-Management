namespace Gym.Application.Common;

/// <summary>Business limits for attendance (BUSINESS_RULES.md §7 Cancel check-in).</summary>
public interface IAttendancePolicy
{
    /// <summary><c>Gym:CancelCheckInWindowMinutes</c>.</summary>
    int CancelWindowMinutes { get; }
}
