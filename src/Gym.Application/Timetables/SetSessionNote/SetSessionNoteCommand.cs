namespace Gym.Application.Timetables.SetSessionNote;

/// <summary>«یادداشت این روز»: the trainer's note on the session of the date with these times, written or changed.</summary>
public sealed record SetSessionNoteCommand(TimeOnly Start, TimeOnly End, string? Text);
