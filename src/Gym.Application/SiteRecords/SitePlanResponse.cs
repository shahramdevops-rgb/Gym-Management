using System.Text.Json.Serialization;

using Gym.Application.Subscriptions;
using Gym.Domain.Payments;
using Gym.Domain.Subscriptions;

namespace Gym.Application.SiteRecords;

/// <summary>
/// One of the member's plans as the site shows it (BUSINESS_RULES.md §15 <i>The member's own
/// records</i>): the panel's <see cref="SubscriptionResponse"/> without the staff's fields. A
/// cancelled plan is marked by <see cref="CancelledAt"/>; the reason the Owner wrote is left out.
/// </summary>
/// <param name="Status">Calculated for the gym's today, as in the panel.</param>
/// <param name="IsSingleSession">A plan has no name (§3): the app labels it from its days and sessions.</param>
public sealed record SitePlanResponse(
    Guid Id,
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
    bool IsSingleSession,
    decimal NetPaid,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentStatus>))] PaymentStatus PaymentStatus)
{
    public static SitePlanResponse From(SubscriptionResponse plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new SitePlanResponse(
            plan.Id,
            plan.Price,
            plan.DurationDays,
            plan.TotalSessions,
            plan.UsedSessions,
            plan.RemainingSessions,
            plan.StartDate,
            plan.EndDate,
            plan.Status,
            plan.FrozenSince,
            plan.TotalFrozenDays,
            plan.CancelledAt,
            plan.IsSingleSession,
            plan.NetPaid,
            plan.PaymentStatus);
    }
}
