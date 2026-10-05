namespace Gym.Application.Reports.GetNeedsAttention;

/// <summary>
/// The members the Owner should call today (BUSINESS_RULES.md §12 <i>Needs attention</i>). Each
/// list is its own question; one member can be on more than one.
/// </summary>
/// <param name="RunningOut">Plans running out with nothing bought after them, the soonest end first.</param>
/// <param name="Left">Members whose last plan ended in the last 30 days, the latest first.</param>
/// <param name="Absent">Members with a live plan who stopped coming, the longest away first.</param>
/// <param name="OldDebts">Members owing on sales more than 30 days old, the largest first.</param>
/// <param name="OldDebtWithoutMember">
/// What walk-ins at the cafe and guests owe on sales more than 30 days old: there is nobody to list.
/// </param>
/// <param name="ChequesDue">
/// Pending cheques the gym gave, dated within 7 days or already past their date, the earliest first
/// (§9 <i>Cheques</i>).
/// </param>
public sealed record NeedsAttentionResponse(
    DateOnly Today,
    IReadOnlyList<RunningOutResponse> RunningOut,
    IReadOnlyList<LeftResponse> Left,
    IReadOnlyList<AbsentResponse> Absent,
    IReadOnlyList<OldDebtResponse> OldDebts,
    decimal OldDebtWithoutMember,
    IReadOnlyList<ChequeDueResponse> ChequesDue);

/// <param name="SessionsLeft">Zero when every session is used and the plan has not reached its end date.</param>
public sealed record RunningOutResponse(
    Guid MemberId, string FullName, string PhoneNumber, int SessionsLeft, DateOnly EndDate);

/// <param name="EndedOn">The last day of the member's last plan.</param>
public sealed record LeftResponse(Guid MemberId, string FullName, string PhoneNumber, DateOnly EndedOn);

/// <param name="LastVisitOn">The day of the member's last visit; <c>null</c> if they never came.</param>
/// <param name="DaysAway">
/// Days since the last visit, or since the plan started when they have not come on this plan.
/// </param>
public sealed record AbsentResponse(
    Guid MemberId, string FullName, string PhoneNumber, DateOnly? LastVisitOn, int DaysAway);

/// <param name="Owed">What the member still owes on sales more than 30 days old.</param>
/// <param name="OldestSaleOn">The day the oldest of those sales was recorded.</param>
public sealed record OldDebtResponse(
    Guid MemberId, string FullName, string PhoneNumber, decimal Owed, DateOnly OldestSaleOn);

/// <param name="DueDate">Before today means past its date and still not marked «پاس شد».</param>
public sealed record ChequeDueResponse(Guid ChequeId, string Payee, decimal Amount, DateOnly DueDate, string Description);
