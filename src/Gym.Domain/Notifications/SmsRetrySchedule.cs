namespace Gym.Domain.Notifications;

/// <summary>
/// When a message that may still pass is tried again (BUSINESS_RULES.md §10 <i>The daily runs</i>):
/// at most <see cref="MaxAttempts"/> tries, with a wait before each one after the first, and never a
/// try that would fall after <see cref="SmsSettings.LatestSendTime"/>.
/// </summary>
/// <remarks>
/// Built from <c>Sms:MaxAttempts</c> and <c>Sms:RetryDelays</c> (3 tries, waiting 1 minute then 5;
/// §0). The numbers live in configuration, not on the settings page: they are about the provider,
/// not about the gym.
/// </remarks>
public sealed class SmsRetrySchedule
{
    private readonly TimeSpan[] _delays;

    /// <param name="maxAttempts">Tries in all, the first one included.</param>
    /// <param name="delays">The wait before the 2nd try, the 3rd, and so on: one fewer than <paramref name="maxAttempts"/>.</param>
    public SmsRetrySchedule(int maxAttempts, IReadOnlyList<TimeSpan> delays)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAttempts);
        ArgumentNullException.ThrowIfNull(delays);

        if (delays.Count != maxAttempts - 1)
        {
            throw new ArgumentException("There is one wait before each try after the first.", nameof(delays));
        }

        if (delays.Any(delay => delay < TimeSpan.Zero))
        {
            throw new ArgumentException("A wait cannot be negative.", nameof(delays));
        }

        MaxAttempts = maxAttempts;
        _delays = [.. delays];
    }

    public int MaxAttempts { get; }

    public IReadOnlyList<TimeSpan> Delays => _delays;

    /// <summary>
    /// How long to wait before the next try, or <c>null</c> when the message is to be given up:
    /// every try is used, or the next one would fall after 22:00.
    /// </summary>
    /// <param name="attemptsMade">Tries already made for the message.</param>
    /// <param name="localTime">The gym's time of day now (Asia/Tehran).</param>
    public TimeSpan? NextDelay(int attemptsMade, TimeOnly localTime)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attemptsMade);

        if (attemptsMade >= MaxAttempts)
        {
            return null;
        }

        var delay = _delays[attemptsMade - 1];

        // TimeSpan, not TimeOnly.Add: a TimeOnly wraps around midnight, and 23:58 is not "early".
        var nextTry = localTime.ToTimeSpan() + delay;

        return nextTry <= SmsSettings.LatestSendTime.ToTimeSpan() ? delay : null;
    }
}
