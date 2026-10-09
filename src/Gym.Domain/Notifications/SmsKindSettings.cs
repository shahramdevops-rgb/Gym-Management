namespace Gym.Domain.Notifications;

/// <summary>
/// One kind's row on the SMS settings page (BUSINESS_RULES.md §10 <i>SMS settings</i>): whether it
/// is on, its number and when it is sent. The text is fixed in <see cref="SmsText"/>.
/// </summary>
/// <param name="Enabled">Off until the Owner turns it on; it can only be on when the rest is filled.</param>
/// <param name="Threshold">
/// The kind's number: days before the end date (<see cref="NotificationKind.SubscriptionExpiring"/>),
/// sessions left (<see cref="NotificationKind.LowSessions"/>), days before the birthday
/// (<see cref="NotificationKind.Birthday"/>) or days before the cheque's or instalment's date
/// (<see cref="NotificationKind.PayableDue"/>). The ranges are in <see cref="SmsSettings.ThresholdRange"/>.
/// </param>
/// <param name="SendTime">The gym's local time the day's run starts at.</param>
public sealed record SmsKindSettings(bool Enabled, int? Threshold, TimeOnly? SendTime)
{
    /// <summary>Everything empty and off: how every kind starts (§10).</summary>
    public static readonly SmsKindSettings Off = new(Enabled: false, Threshold: null, SendTime: null);

    /// <summary>The number and the time are both there. A method, so it is not sent as JSON.</summary>
    public bool IsFilled() => Threshold is not null && SendTime is not null;
}
