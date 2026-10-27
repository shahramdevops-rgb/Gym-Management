namespace Gym.Application.Notifications;

/// <summary>
/// The welcome back's row on the settings page as the page sends it (BUSINESS_RULES.md §10 <i>The
/// welcome back</i>, task 10.20): only on or off. It goes to the member's own number, after their
/// first visit since the absence SMS.
/// </summary>
public sealed record WelcomeBackSettings(bool Enabled);

/// <param name="DelayMinutes">30, fixed: how long after the check-in it goes.</param>
public sealed record WelcomeBackSettingsResponse(bool Enabled, int DelayMinutes);
