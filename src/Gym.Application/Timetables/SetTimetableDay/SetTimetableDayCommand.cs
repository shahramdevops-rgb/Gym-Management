namespace Gym.Application.Timetables.SetTimetableDay;

/// <summary>«فقط این روز»: one date's sessions, whatever the pattern says. None closes the day.</summary>
public sealed record SetTimetableDayCommand(IReadOnlyList<SessionItem> Sessions);

/// <param name="IsVip">Left out, the session is a normal one (15.6e): a page from before VIP still saves.</param>
public sealed record SessionItem(TimeOnly Start, TimeOnly End, int Capacity, bool IsVip = false);
