namespace Gym.Application.Timetables.SetTimetableDay;

/// <summary>«فقط این روز»: one date's sessions, whatever the pattern says. None closes the day.</summary>
public sealed record SetTimetableDayCommand(IReadOnlyList<SessionItem> Sessions);

public sealed record SessionItem(TimeOnly Start, TimeOnly End, int Capacity);
