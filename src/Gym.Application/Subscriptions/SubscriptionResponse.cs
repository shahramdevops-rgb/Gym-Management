using System.Text.Json.Serialization;

using Gym.Domain.Payments;
using Gym.Domain.Subscriptions;

namespace Gym.Application.Subscriptions;

/// <param name="Status">Calculated for the gym's today when the response was built; never stored.</param>
/// <param name="IsSingleSession">One visit, today only (BUSINESS_RULES.md §4). The front desk shows
/// "تک‌جلسه‌ای" instead of a session count for these, because 1 of 1 is not progress worth a bar.
/// A plan has no name (§3): the frontend labels it from its days and sessions.</param>
/// <param name="NetPaid">Payments minus refunds for this subscription (BUSINESS_RULES.md §4).</param>
/// <param name="PaymentStatus">Calculated from <see cref="NetPaid"/> and <see cref="Price"/>; never stored.</param>
public sealed record SubscriptionResponse(
    Guid Id,
    Guid MemberId,
    decimal Price,
    int DurationDays,
    int TotalSessions,
    int UsedSessions,
    int RemainingSessions,
    DateOnly StartDate,
    DateOnly EndDate,
    [property: JsonConverter(typeof(JsonStringEnumConverter<SubscriptionStatus>))] SubscriptionStatus Status,
    DateOnly? FrozenSince,
    int TotalFrozenDays,
    DateTimeOffset? CancelledAt,
    string? CancellationReason,
    bool IsSingleSession,
    uint Version,
    DateTimeOffset CreatedAt,
    decimal NetPaid,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentStatus>))] PaymentStatus PaymentStatus)
{
    public static SubscriptionResponse From(Subscription subscription, DateOnly today, decimal netPaid)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return new SubscriptionResponse(
            subscription.Id,
            subscription.MemberId,
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
            subscription.IsSingleSession,
            subscription.Version,
            subscription.CreatedAt,
            netPaid,
            PaymentStatusCalculator.Calculate(subscription.Price, netPaid));
    }
}
