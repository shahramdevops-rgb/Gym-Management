namespace Gym.Application.Notifications;

/// <summary>
/// The welcome's row on the settings page as the page sends it (BUSINESS_RULES.md §10 <i>The
/// welcome</i>): only on or off. It goes to the member's own number, at the sale of their first plan.
/// </summary>
public sealed record WelcomeSettings(bool Enabled);

/// <param name="MorningSendTime">10:00, fixed: when a welcome for a sale made after 22:00 goes.</param>
public sealed record WelcomeSettingsResponse(bool Enabled, TimeOnly MorningSendTime);
