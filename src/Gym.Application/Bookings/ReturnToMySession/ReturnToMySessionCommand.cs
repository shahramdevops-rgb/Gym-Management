namespace Gym.Application.Bookings.ReturnToMySession;

/// <summary>«می‌آیم»: the member back in the session of <paramref name="Date"/> with these times, which they left.</summary>
public sealed record ReturnToMySessionCommand(DateOnly Date, TimeOnly Start, TimeOnly End);
