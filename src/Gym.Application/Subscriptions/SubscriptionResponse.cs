using System.Text.Json.Serialization;

using Gym.Domain.Subscriptions;

namespace Gym.Application.Subscriptions;

/// <param name="Status">Calculated for the gym's today when the response was built; never stored.</param>
/// <param name="TotalSessions"><c>null</c> means unlimited.</param>
/// <param name="RemainingSessions"><c>null</c> means unlimited.</param>
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
    DateTimeOffset CreatedAt)
{
    public static SubscriptionResponse From(Subscription subscription, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return new SubscriptionResponse(
            subscription.Id,
            subscription.MemberId,
            subscription.PlanId,
            subscription.PlanName,
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
            subscription.CreatedAt);
    }
}
