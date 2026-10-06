namespace Gym.Application.Notifications.SendDailySms;

/// <summary>How one daily run went: what the log says and what the tests check.</summary>
/// <param name="Ran">
/// <c>false</c> when the run sent nothing because all SMS or its kind is off, or it is outside the
/// sending hours.
/// </param>
/// <param name="Interrupted">Rows a previous run left <c>Pending</c>, now <c>Unknown</c>.</param>
/// <param name="CreditExhausted">The provider said the credit is used up, and the run stopped there.</param>
public sealed record SmsRunResult(
    bool Ran,
    int Sent = 0,
    int Failed = 0,
    int Unknown = 0,
    int Interrupted = 0,
    bool CreditExhausted = false)
{
    public static readonly SmsRunResult NotRun = new(Ran: false);
}
