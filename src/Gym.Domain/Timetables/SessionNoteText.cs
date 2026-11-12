namespace Gym.Domain.Timetables;

/// <summary>
/// The text of a trainer's note on a session (BUSINESS_RULES.md §15 <i>Session notes</i>, task 15.7e),
/// either kind: the standing note on a session of the timetable («یادداشت ثابت») and the note on
/// one date's session («یادداشت این روز», <see cref="SessionNote"/>).
/// </summary>
public static class SessionNoteText
{
    /// <summary>A standing note is short: it shows on the session in every list (proposed by Claude, 1405/08/20).</summary>
    public const int MaxStandingLength = 100;

    /// <summary>A date's note may say more, e.g. what to bring that day (proposed by Claude, 1405/08/20).</summary>
    public const int MaxDateLength = 300;

    /// <summary>The text as kept: trimmed, and <c>null</c> when nothing is left.</summary>
    public static string? Normalize(string? text)
    {
        var trimmed = text?.Trim();

        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
