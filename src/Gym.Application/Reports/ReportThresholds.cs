namespace Gym.Application.Reports;

/// <summary>
/// The numbers the operational reports judge by (BUSINESS_RULES.md §12 <i>Operational reports</i>,
/// <i>Needs attention</i>), named once so the reports, their tests and the documentation agree.
/// </summary>
public static class ReportThresholds
{
    /// <summary>
    /// A plan is running out with this many sessions left or fewer: the board's own number (§7
    /// <i>The "currently inside" board</i>), so the dashboard and the desk agree.
    /// </summary>
    public const int LowSessions = 3;

    /// <summary>A plan is running out when it ends within this many days: the board's own number too.</summary>
    public const int ExpiringWithinDays = 5;

    /// <summary>A member with a live plan and no visit for this many days has stopped coming.</summary>
    public const int AbsentDays = 10;

    /// <summary>
    /// A plan ended this many days ago or less, with nothing after it, is a member who just left.
    /// The same window decides whether a plan was renewed.
    /// </summary>
    public const int RenewalWindowDays = 30;

    /// <summary>A debt on a sale older than this many days is an old debt (§12 <i>Receivables</i>).</summary>
    public const int OldDebtDays = 30;

    /// <summary>
    /// A pending cheque is on the dashboard this many days before its date (§9 <i>Cheques</i>):
    /// one number for every cheque, decided with the developer.
    /// </summary>
    public const int ChequeDueWithinDays = 7;
}
