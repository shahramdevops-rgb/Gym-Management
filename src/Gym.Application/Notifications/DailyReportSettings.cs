namespace Gym.Application.Notifications;

/// <summary>
/// The daily report's row on the settings page as the page sends it (BUSINESS_RULES.md §10 <i>The
/// daily report</i>): on or off, where it goes, and when (task 10.17: chosen by the Owner).
/// </summary>
/// <param name="Phone">As typed (any digits, <c>09…</c> or <c>+98…</c>); empty clears it.</param>
/// <param name="SendTime">08:00 to 23:45, on a quarter hour.</param>
public sealed record DailyReportSettings(bool Enabled, string? Phone, TimeOnly SendTime);

/// <param name="Phone">E.164 (<c>+989…</c>), or <c>null</c> until the Owner enters it.</param>
/// <param name="SendTime">When it goes, chosen by the Owner (task 10.17).</param>
public sealed record DailyReportSettingsResponse(bool Enabled, string? Phone, TimeOnly SendTime);
