namespace Gym.Application.Notifications.SendSessionCancelledSms;

/// <summary>What became of one student's SMS job about a cancelled session, for the log and the tests.</summary>
public enum SessionCancelledSmsOutcome
{
    Sent,

    /// <summary>Refused, the credit used up, or every try used: the Owner can resend it.</summary>
    Failed,

    /// <summary>The request left and no answer came: it may have arrived.</summary>
    Unknown,

    /// <summary>A job stopped mid-send left the row <c>Pending</c>; it is now <c>Unknown</c>.</summary>
    Interrupted,

    /// <summary>The student already had an SMS about this change.</summary>
    AlreadyWritten,

    /// <summary>All SMS or the cancelled session's SMS has been turned off since the save.</summary>
    Off,

    /// <summary>Outside 08:00–22:00: put off until 08:00.</summary>
    Deferred,

    /// <summary>
    /// No session of the change is still worth an SMS (brought back, started, or seen in the app), the
    /// student is no longer the trainer's, or the member was deactivated.
    /// </summary>
    NoLongerDue,
}
