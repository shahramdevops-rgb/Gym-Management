namespace Gym.Domain.Bookings;

/// <summary>Where a <see cref="LeaveRequest"/> stands. Stored as its name.</summary>
public enum LeaveRequestStatus
{
    /// <summary>Waiting for the trainer; read as declined once the session has started.</summary>
    Pending = 1,

    /// <summary>The trainer accepted: the student is out of the date and the place is free.</summary>
    Accepted = 2,

    /// <summary>The trainer declined: the student is in the session as before.</summary>
    Declined = 3,

    /// <summary>The student took it back: a waiting one, or an accepted one with «می‌آیم».</summary>
    Cancelled = 4,
}
