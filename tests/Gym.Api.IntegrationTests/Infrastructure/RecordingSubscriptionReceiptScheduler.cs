using Gym.Application.Common.Sms;

namespace Gym.Api.IntegrationTests.Infrastructure;

/// <summary>
/// The test host's <see cref="ISubscriptionReceiptScheduler"/>: it remembers each receipt it was
/// asked for and sends nothing.
/// </summary>
/// <remarks>
/// The test host runs a real Hangfire server against the test database. A receipt enqueued by a
/// test's payment would be sent in the middle of some later test, or after the database was reset.
/// The tests call the receipt handler themselves, on a clock they choose.
/// </remarks>
internal sealed class RecordingSubscriptionReceiptScheduler : ISubscriptionReceiptScheduler
{
    private readonly List<(Guid SubscriptionId, DateTimeOffset? At)> _asked = [];
    private readonly Lock _lock = new();

    /// <summary>Each receipt asked for, oldest first: <c>At</c> is <c>null</c> for one sent at once.</summary>
    public IReadOnlyList<(Guid SubscriptionId, DateTimeOffset? At)> Asked
    {
        get
        {
            lock (_lock)
            {
                return [.. _asked];
            }
        }
    }

    public void SendNow(Guid subscriptionId)
    {
        lock (_lock)
        {
            _asked.Add((subscriptionId, null));
        }
    }

    public void SendAt(Guid subscriptionId, DateTimeOffset at)
    {
        lock (_lock)
        {
            _asked.Add((subscriptionId, at));
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _asked.Clear();
        }
    }
}
