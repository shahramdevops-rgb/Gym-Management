namespace Gym.Application.Notifications;

/// <summary>
/// The cancelled session's SMS row on the settings page as the page sends it (BUSINESS_RULES.md §15
/// <i>Timetable and attendance</i>, task 15.6c): only on or off. It goes to the student when their
/// trainer cancels a session they are in.
/// </summary>
public sealed record SessionCancelledSettings(bool Enabled);

/// <param name="OpeningSendTime">08:00, fixed: when the SMS for a session cancelled after 22:00 goes.</param>
/// <param name="DaysAhead">7, fixed: only a session from today to this many days after it gets an SMS.</param>
public sealed record SessionCancelledSettingsResponse(bool Enabled, TimeOnly OpeningSendTime, int DaysAhead);
