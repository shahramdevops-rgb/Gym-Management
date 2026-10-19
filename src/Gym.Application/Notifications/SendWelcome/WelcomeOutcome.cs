namespace Gym.Application.Notifications.SendWelcome;

/// <summary>What became of one welcome job, for the log and the tests.</summary>
public enum WelcomeOutcome
{
    Sent,

    /// <summary>Refused, the credit used up, or every try used: the Owner can resend it.</summary>
    Failed,

    /// <summary>The request left and no answer came: it may have arrived.</summary>
    Unknown,

    /// <summary>A job stopped mid-send left the row <c>Pending</c>; it is now <c>Unknown</c>.</summary>
    Interrupted,

    /// <summary>The member already has their welcome.</summary>
    AlreadyWritten,

    /// <summary>All SMS or the welcome has been turned off since the sale.</summary>
    Off,

    /// <summary>Outside 08:00–22:00: put off until 10:00.</summary>
    Deferred,

    /// <summary>The plan cancelled, or the member deactivated, since the sale.</summary>
    NoLongerDue,
}
