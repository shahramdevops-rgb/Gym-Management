namespace Gym.Application.Bookings.CancelMyLeave;

/// <summary>«لغو درخواست»: the member takes back their waiting leave request for the session of <paramref name="Date"/> with these times.</summary>
public sealed record CancelMyLeaveCommand(DateOnly Date, TimeOnly Start, TimeOnly End);
