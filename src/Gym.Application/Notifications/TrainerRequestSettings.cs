namespace Gym.Application.Notifications;

/// <summary>
/// The trainer's SMS row on the settings page as the page sends it (BUSINESS_RULES.md §15 <i>A
/// member and their trainer</i>, task 15.4c): only on or off. It goes to the trainer's own number
/// when a member picks them on the site.
/// </summary>
public sealed record TrainerRequestSettings(bool Enabled);

/// <param name="MorningSendTime">10:00, fixed: when the SMS for a request made after 22:00 goes.</param>
public sealed record TrainerRequestSettingsResponse(bool Enabled, TimeOnly MorningSendTime);
