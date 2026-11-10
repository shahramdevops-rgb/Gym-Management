namespace Gym.Application.Timetables.RemoveSessionNote;

/// <summary>«پاک کردن یادداشت»: the note on the session of the date with these times goes.</summary>
public sealed record RemoveSessionNoteCommand(TimeOnly Start, TimeOnly End);
