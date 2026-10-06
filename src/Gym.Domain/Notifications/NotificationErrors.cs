using Gym.Domain.Common;

namespace Gym.Domain.Notifications;

public static class NotificationErrors
{
    public static readonly Error NotPending = Error.Conflict(
        "Notifications.NotPending",
        "The message is no longer waiting to be sent.");

    public static readonly Error NotSent = Error.Conflict(
        "Notifications.NotSent",
        "Only a sent message has a delivery.");
}
