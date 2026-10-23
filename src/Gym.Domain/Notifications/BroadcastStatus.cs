namespace Gym.Domain.Notifications;

/// <summary>Where a broadcast stands (BUSINESS_RULES.md §10 <i>The broadcast</i>).</summary>
public enum BroadcastStatus
{
    /// <summary>Its job is writing and sending the messages. At most one broadcast at a time.</summary>
    Sending,

    /// <summary>Every recipient read when it started, or when it was continued, has had their message written.</summary>
    Finished,

    /// <summary>«متوقف شد»: it stopped before everyone was written (<see cref="BroadcastStopReason"/>); the Owner can continue it.</summary>
    Stopped,
}

/// <summary>Why a broadcast stopped before everyone was written.</summary>
public enum BroadcastStopReason
{
    /// <summary>The provider said the credit is used up (418).</summary>
    CreditExhausted,

    /// <summary>22:00 came while it was sending, or the job ran outside 08:00–22:00 (a restart late in the evening).</summary>
    OutsideSendingHours,

    /// <summary>All SMS or the broadcast was turned off, before the job ran or while it was sending.</summary>
    TurnedOff,

    /// <summary>Something unexpected went wrong in the job, or the job itself failed; it is in the logs.</summary>
    Error,

    /// <summary>
    /// A member with a longer first name joined the group before the job wrote the messages, and the
    /// text no longer fits in 3 parts for them; nothing more is written.
    /// </summary>
    TooLong,
}
