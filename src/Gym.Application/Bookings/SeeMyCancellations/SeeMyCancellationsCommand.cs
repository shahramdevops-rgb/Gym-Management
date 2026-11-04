namespace Gym.Application.Bookings.SeeMyCancellations;

/// <param name="UpTo">The <c>latest</c> of the notice the member read: what was saved after it stays.</param>
public sealed record SeeMyCancellationsCommand(DateTimeOffset UpTo);
