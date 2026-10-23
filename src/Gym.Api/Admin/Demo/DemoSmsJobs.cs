using Gym.Application.Common.Sms;
using Gym.Domain.Notifications;

namespace Gym.Api.Admin.Demo;

/// <summary>
/// The receipts (task 10.9) and welcomes (task 10.10) <c>admin seed-demo</c> was asked for, kept until
/// the seeder sends them on its own clock. Hangfire would run them at the real time, after the demo is
/// over, so the seeder sends each one itself: at once after the sale or payment that asked for it, or
/// at its 10:00.
/// </summary>
public sealed class DemoSmsJobs : ISubscriptionReceiptScheduler, IWelcomeScheduler
{
    private readonly List<(NotificationKind Kind, Guid SubscriptionId, DateTimeOffset? At)> _waiting = [];

    void ISubscriptionReceiptScheduler.SendNow(Guid subscriptionId) =>
        _waiting.Add((NotificationKind.SubscriptionReceipt, subscriptionId, null));

    void ISubscriptionReceiptScheduler.SendAt(Guid subscriptionId, DateTimeOffset at) =>
        _waiting.Add((NotificationKind.SubscriptionReceipt, subscriptionId, at));

    void IWelcomeScheduler.SendNow(Guid subscriptionId) =>
        _waiting.Add((NotificationKind.Welcome, subscriptionId, null));

    void IWelcomeScheduler.SendAt(Guid subscriptionId, DateTimeOffset at) =>
        _waiting.Add((NotificationKind.Welcome, subscriptionId, at));

    /// <summary>
    /// The messages due by <paramref name="now"/>, oldest time first, taken off the list. One asked for
    /// at once is due at the moment it was asked, given as <paramref name="now"/>.
    /// </summary>
    public List<(NotificationKind Kind, Guid SubscriptionId, DateTimeOffset At)> TakeDue(DateTimeOffset now)
    {
        var due = _waiting
            .Where(job => job.At is null || job.At <= now)
            .Select(job => (job.Kind, job.SubscriptionId, At: job.At ?? now))
            .OrderBy(job => job.At)
            .ToList();

        _waiting.RemoveAll(job => job.At is null || job.At <= now);

        return due;
    }
}
