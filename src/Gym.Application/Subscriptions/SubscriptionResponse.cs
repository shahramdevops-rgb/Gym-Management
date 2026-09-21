using System.Text.Json.Serialization;

using Gym.Domain.Payments;
using Gym.Domain.Subscriptions;

namespace Gym.Application.Subscriptions;

/// <param name="Status">Calculated for the gym's today when the response was built; never stored.</param>
/// <param name="PlanName">The plan's name <i>now</i>, not when it was sold: renaming a plan corrects the
/// label everywhere (BUSINESS_RULES.md §4). It is a parameter rather than something the subscription
/// carries, so every caller has to fetch it and none can silently serve a stale one.</param>
/// <param name="TotalSessions"><c>null</c> means unlimited.</param>
/// <param name="RemainingSessions"><c>null</c> means unlimited.</param>
/// <param name="NetPaid">Payments minus refunds for this subscription (BUSINESS_RULES.md §4).</param>
/// <param name="PaymentStatus">Calculated from <see cref="NetPaid"/> and <see cref="Price"/>; never stored.</param>
public sealed record SubscriptionResponse(
    Guid Id,
    Guid MemberId,
    Guid PlanId,
    string PlanName,
    decimal Price,
    int DurationDays,
    int? TotalSessions,
    int UsedSessions,
    int? RemainingSessions,
    DateOnly StartDate,
    DateOnly EndDate,
    [property: JsonConverter(typeof(JsonStringEnumConverter<SubscriptionStatus>))] SubscriptionStatus Status,
    DateOnly? FrozenSince,
    int TotalFrozenDays,
    DateTimeOffset? CancelledAt,
    string? CancellationReason,
    uint Version,
    DateTimeOffset CreatedAt,
    decimal NetPaid,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentStatus>))] PaymentStatus PaymentStatus)
{
    public static SubscriptionResponse From(Subscription subscription, string planName, DateOnly today, decimal netPaid)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return new SubscriptionResponse(
            subscription.Id,
            subscription.MemberId,
            subscription.PlanId,
            planName,
            subscription.Price,
            subscription.DurationDays,
            subscription.TotalSessions,
            subscription.UsedSessions,
            subscription.RemainingSessions,
            subscription.StartDate,
            subscription.EndDate,
            subscription.GetStatus(today),
            subscription.FrozenSince,
            subscription.TotalFrozenDays,
            subscription.CancelledAt,
            subscription.CancellationReason,
            subscription.Version,
            subscription.CreatedAt,
            netPaid,
            PaymentStatusCalculator.Calculate(subscription.Price, netPaid));
    }
}
