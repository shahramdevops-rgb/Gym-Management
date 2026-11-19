namespace Gym.Domain.Coaching;

/// <summary>Where a <see cref="RenewalRequest"/> stands (BUSINESS_RULES.md §15 <i>The coaching period</i>). Stored as its name.</summary>
public enum RenewalRequestStatus
{
    /// <summary>Waiting for the trainer's answer. A link has one at most.</summary>
    Pending = 1,

    /// <summary>
    /// The trainer renewed: «تمدید» from «درخواست‌ها» or on «برنامهٔ ثابت», or a «ویرایش دوره» that
    /// moved the last day later.
    /// </summary>
    Renewed = 2,

    /// <summary>The trainer declined; the member reads it while the last day is the one asked about.</summary>
    Declined = 3,

    /// <summary>The member took it back («لغو درخواست»).</summary>
    Cancelled = 4,

    /// <summary>The coaching ended while it was waiting, however it ended.</summary>
    LinkEnded = 5,
}
