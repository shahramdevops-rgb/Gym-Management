namespace Gym.Application.Notifications;

/// <summary>
/// The daily report's row on the settings page as the page sends it (BUSINESS_RULES.md §10 <i>The
/// daily report</i>): on or off, and where it goes. The time is fixed in the code.
/// </summary>
/// <param name="Phone">As typed (any digits, <c>09…</c> or <c>+98…</c>); empty clears it.</param>
public sealed record DailyReportSettings(bool Enabled, string? Phone);

/// <param name="Phone">E.164 (<c>+989…</c>), or <c>null</c> until the Owner enters it.</param>
/// <param name="SendTime">22:45, fixed: sent so the page shows the time the server really uses.</param>
public sealed record DailyReportSettingsResponse(bool Enabled, string? Phone, TimeOnly SendTime);
