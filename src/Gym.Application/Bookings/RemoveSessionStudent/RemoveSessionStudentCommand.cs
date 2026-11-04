namespace Gym.Application.Bookings.RemoveSessionStudent;

/// <summary>«حذف از این روز»: the student out of the session of the date with these times.</summary>
public sealed record RemoveSessionStudentCommand(Guid MemberId, TimeOnly Start, TimeOnly End);
