using Gym.Application.Common.Sms;

namespace Gym.Api.IntegrationTests.Infrastructure;

/// <summary>
/// The test host's schedulers for the messages sent one at a time (the receipt, the welcome): each
/// remembers what it was asked for and sends nothing.
/// </summary>
/// <remarks>
/// The test host runs a real Hangfire server against the test database. A message enqueued by a
/// test's sale or payment would be sent in the middle of some later test, or after the database was
/// reset. The tests call the sending handler themselves, on a clock they choose.
/// </remarks>
internal abstract class RecordingSmsJobScheduler
{
    private readonly List<(Guid SubscriptionId, DateTimeOffset? At)> _asked = [];
    private readonly Lock _lock = new();

    /// <summary>Each message asked for, oldest first: <c>At</c> is <c>null</c> for one sent at once.</summary>
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

/// <summary>The receipts asked for (task 10.9).</summary>
internal sealed class RecordingSubscriptionReceiptScheduler : RecordingSmsJobScheduler, ISubscriptionReceiptScheduler;

/// <summary>The welcomes asked for (task 10.10).</summary>
internal sealed class RecordingWelcomeScheduler : RecordingSmsJobScheduler, IWelcomeScheduler;

/// <summary>
/// The broadcasts asked for (task 10.13). <see cref="Fail"/> makes the next ask throw, as a job
/// runner that cannot take it would.
/// </summary>
internal sealed class RecordingBroadcastScheduler : IBroadcastScheduler
{
    private readonly List<Guid> _asked = [];
    private readonly Lock _lock = new();

    public bool Fail { get; set; }

    public IReadOnlyList<Guid> Asked
    {
        get
        {
            lock (_lock)
            {
                return [.. _asked];
            }
        }
    }

    public void SendNow(Guid broadcastId)
    {
        if (Fail)
        {
            throw new InvalidOperationException("The job runner is down.");
        }

        lock (_lock)
        {
            _asked.Add(broadcastId);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _asked.Clear();
            Fail = false;
        }
    }
}
