namespace Gym.Application.Bookings.LeaveMySession;

/// <summary>«نمی‌آیم»: the member out of the session of <paramref name="Date"/> with these times.</summary>
public sealed record LeaveMySessionCommand(DateOnly Date, TimeOnly Start, TimeOnly End);
