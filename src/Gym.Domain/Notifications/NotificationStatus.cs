namespace Gym.Domain.Notifications;

/// <summary>Where one SMS stands (BUSINESS_RULES.md §10 <i>Sending</i>).</summary>
public enum NotificationStatus
{
    /// <summary>Not sent yet, or a failure that may pass and will be tried again.</summary>
    Pending,

    /// <summary>The provider accepted it, and it was paid for.</summary>
    Sent,

    /// <summary>It will not be tried again by itself: a failure that will not pass, or no tries left.</summary>
    Failed,

    /// <summary>
    /// The request left but no answer came back, so it may have gone and been paid for. Never
    /// retried by itself: the template method has no duplicate guard on the provider's side.
    /// </summary>
    Unknown,
}
