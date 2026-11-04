namespace Gym.Domain.Bookings;

/// <summary>Who changed a student's sessions (BUSINESS_RULES.md §15 <i>Who puts a member in a session</i>).</summary>
public enum BookedBy
{
    Trainer = 1,
    Member = 2,
}
