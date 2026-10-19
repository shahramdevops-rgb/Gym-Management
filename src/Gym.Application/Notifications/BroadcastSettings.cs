namespace Gym.Application.Notifications;

/// <summary>
/// The broadcast's row on the settings page (BUSINESS_RULES.md §10 <i>The broadcast</i>, task 10.13):
/// only on or off. The text and the group are chosen each time on «پیامک گروهی».
/// </summary>
public sealed record BroadcastSettings(bool Enabled);
