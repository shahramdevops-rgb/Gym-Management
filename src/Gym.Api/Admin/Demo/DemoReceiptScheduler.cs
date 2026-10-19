using Gym.Application.Common.Sms;

namespace Gym.Api.Admin.Demo;

/// <summary>
/// The receipts <c>admin seed-demo</c> was asked for (task 10.9), kept until the seeder sends them on
/// its own clock. Hangfire would run them at the real time, after the demo is over, so the seeder
/// sends each one itself: at once after the payment that asked for it, or at its 10:00.
/// </summary>
public sealed class DemoReceiptScheduler : ISubscriptionReceiptScheduler
{
    private readonly List<(Guid SubscriptionId, DateTimeOffset? At)> _waiting = [];

    public void SendNow(Guid subscriptionId) => _waiting.Add((subscriptionId, null));

    public void SendAt(Guid subscriptionId, DateTimeOffset at) => _waiting.Add((subscriptionId, at));

    /// <summary>
    /// The receipts due by <paramref name="now"/>, oldest time first, taken off the list. One asked for
    /// at once is due at the moment it was asked, given as <paramref name="now"/>.
    /// </summary>
    public List<(Guid SubscriptionId, DateTimeOffset At)> TakeDue(DateTimeOffset now)
    {
        var due = _waiting
            .Where(receipt => receipt.At is null || receipt.At <= now)
            .Select(receipt => (receipt.SubscriptionId, At: receipt.At ?? now))
            .OrderBy(receipt => receipt.At)
            .ToList();

        _waiting.RemoveAll(receipt => receipt.At is null || receipt.At <= now);

        return due;
    }
}
