namespace Gym.Application.Bookings.MoveMySession;

/// <summary>«جابه‌جایی»: the member's session of <paramref name="Date"/> to the one of <paramref name="ToDate"/> with the <c>To</c> times.</summary>
public sealed record MoveMySessionCommand(DateOnly Date, TimeOnly Start, TimeOnly End, DateOnly ToDate, TimeOnly ToStart, TimeOnly ToEnd);
