namespace Gym.Application.Subscriptions;

/// <summary>
/// What a subscription sold, for the lists that show one next to other things (the debt breakdown,
/// the payment history). A plan has no name since task 6.5.6 (BUSINESS_RULES.md §3): it reads as its
/// numbers, «۳۰ روز · ۱۲ جلسه», or «تک‌جلسه‌ای». The frontend builds that Persian label from these
/// three fields, so the API sends numbers and never display text.
/// </summary>
public sealed record PlanSummary(int DurationDays, int TotalSessions, bool IsSingleSession);
