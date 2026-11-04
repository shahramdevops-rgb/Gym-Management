namespace Gym.Domain.Bookings;

/// <summary>
/// Why a student is out of one date of their fixed schedule (<see cref="SessionSkip"/>). Only a
/// date they left themselves comes back with «می‌آیم» (BUSINESS_RULES.md §15 <i>Students in sessions</i>).
/// </summary>
public enum SkipReason
{
    /// <summary>«نمی‌آیم», by the member.</summary>
    Left = 1,

    /// <summary>The member moved it to another session, where a <see cref="SessionBooking"/> holds them.</summary>
    Moved = 2,

    /// <summary>The trainer took them out of that date.</summary>
    TakenOut = 3,

    /// <summary>The trainer's saving or ending the schedule left it out: no such session then, or it had started.</summary>
    PassedOver = 4,
}
