namespace Gym.Application.Bookings.LeaveMySession;

/// <summary>«درخواست مرخصی» (15.7i): leave from the session of <paramref name="Date"/> with these times, asked of the trainer.</summary>
public sealed record LeaveMySessionCommand(DateOnly Date, TimeOnly Start, TimeOnly End);
