namespace Gym.Application.Notifications.SendTrainerRequestSms;

/// <summary>What became of one trainer's SMS job, for the log and the tests.</summary>
public enum TrainerRequestSmsOutcome
{
    Sent,

    /// <summary>Refused, the credit used up, or every try used: the Owner can resend it.</summary>
    Failed,

    /// <summary>The request left and no answer came: it may have arrived.</summary>
    Unknown,

    /// <summary>A job stopped mid-send left the row <c>Pending</c>; it is now <c>Unknown</c>.</summary>
    Interrupted,

    /// <summary>The trainer already had an SMS about this member today.</summary>
    AlreadyWritten,

    /// <summary>All SMS or the trainer's SMS has been turned off since the request.</summary>
    Off,

    /// <summary>Outside 08:00–22:00: put off until 10:00.</summary>
    Deferred,

    /// <summary>
    /// The request is no longer waiting (taken back, replaced, answered in the app), the trainer was
    /// stopped, or the member deactivated, before the SMS went.
    /// </summary>
    NoLongerDue,
}
