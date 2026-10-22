using Gym.Domain.Subscriptions;

namespace Gym.Domain.Notifications;

/// <summary>
/// «جلسات رو به اتمام» by the plan's size (BUSINESS_RULES.md §10 <i>The four kinds</i>, decided with
/// the developer, 1405/08/01, task 10.17): plans of 5 sessions up to <see cref="SmallPlanMax"/> get
/// it at <see cref="SmallThreshold"/> sessions left, plans up to <see cref="MediumPlanMax"/> at
/// <see cref="MediumThreshold"/>, and bigger ones at <see cref="LargeThreshold"/>.
/// </summary>
/// <remarks>
/// A kind of its own rather than <see cref="SmsKindSettings"/>: it has five numbers, not one. Like
/// the other kinds, any of them may be empty while it is off, and it is on only when all are filled.
/// </remarks>
/// <param name="SmallPlanMax">The largest plan of the first row; it starts at the smallest plan, 5.</param>
/// <param name="MediumPlanMax">The largest plan of the second row; bigger plans are the third.</param>
public sealed record LowSessionsSettings(
    bool Enabled,
    int? SmallPlanMax,
    int? MediumPlanMax,
    int? SmallThreshold,
    int? MediumThreshold,
    int? LargeThreshold,
    TimeOnly? SendTime)
{
    /// <summary>The fewest sessions left a row can be set to.</summary>
    public const int MinThreshold = 1;

    /// <summary>The most sessions left a row can be set to (the developer: 1 to 3, and 4 to be safe).</summary>
    public const int MaxThreshold = 4;

    /// <summary>The first row's largest plan: at least the smallest plan, and room left for the second row.</summary>
    public const int SmallPlanMaxFloor = Subscription.MinSessionCount;

    public const int SmallPlanMaxCeiling = Subscription.MaxSessionCount - 1;

    /// <summary>The second row's largest plan may be the largest plan there is; the third row is then empty.</summary>
    public const int MediumPlanMaxCeiling = Subscription.MaxSessionCount;

    public static readonly LowSessionsSettings Off = new(
        Enabled: false, SmallPlanMax: null, MediumPlanMax: null, SmallThreshold: null, MediumThreshold: null,
        LargeThreshold: null, SendTime: null);

    public bool IsFilled() =>
        SmallPlanMax is not null && MediumPlanMax is not null && SmallThreshold is not null
        && MediumThreshold is not null && LargeThreshold is not null && SendTime is not null;

    /// <summary>
    /// The sessions left at which a plan of <paramref name="totalSessions"/> gets the SMS, or
    /// <c>null</c> while the rows are not filled.
    /// </summary>
    public int? ThresholdFor(int totalSessions) =>
        totalSessions <= SmallPlanMax ? SmallThreshold
        : totalSessions <= MediumPlanMax ? MediumThreshold
        : SmallPlanMax is null || MediumPlanMax is null ? null
        : LargeThreshold;

    /// <summary>The largest of the three, to narrow the run's query; the domain rule decides each plan.</summary>
    public int? HighestThreshold() =>
        IsFilled() ? Math.Max(SmallThreshold!.Value, Math.Max(MediumThreshold!.Value, LargeThreshold!.Value)) : null;
}
