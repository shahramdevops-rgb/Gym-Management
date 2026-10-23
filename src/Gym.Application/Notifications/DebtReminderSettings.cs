namespace Gym.Application.Notifications;

/// <summary>
/// The debt reminder's row on the settings page (BUSINESS_RULES.md §10 <i>The debt reminder</i>,
/// task 10.11), as the page sends it and reads it back: its own record, since it has a minimum
/// amount the other kinds do not.
/// </summary>
/// <param name="DaysAfter">Days since the oldest unpaid sale, 1 to 90.</param>
/// <param name="SendTime">The gym's local time the day's run starts at.</param>
/// <param name="MinAmount">In Toman: only a whole debt of at least this is reminded.</param>
public sealed record DebtReminderSettings(bool Enabled, int? DaysAfter, TimeOnly? SendTime, decimal? MinAmount);
