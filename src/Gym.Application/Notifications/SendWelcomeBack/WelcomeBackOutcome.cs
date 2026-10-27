namespace Gym.Application.Notifications.SendWelcomeBack;

/// <summary>What became of one welcome back job, for the log and the tests.</summary>
public enum WelcomeBackOutcome
{
    Sent,

    /// <summary>Refused, the credit used up, or every try used: the Owner can resend it.</summary>
    Failed,

    /// <summary>The request left and no answer came: it may have arrived.</summary>
    Unknown,

    /// <summary>A job stopped mid-send left the row <c>Pending</c>; it is now <c>Unknown</c>.</summary>
    Interrupted,

    /// <summary>This absence already has its welcome back.</summary>
    AlreadyWritten,

    /// <summary>All SMS or the welcome back has been turned off since the check-in.</summary>
    Off,

    /// <summary>Before 08:00: put off until 08:00 that day.</summary>
    Deferred,

    /// <summary>The check-in's day is over: the next day it would no longer be about coming back today.</summary>
    TooLate,

    /// <summary>The check-in cancelled, no longer the first visit after the absence SMS, or the member deactivated.</summary>
    NoLongerDue,
}
