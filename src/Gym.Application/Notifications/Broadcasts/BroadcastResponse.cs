using System.Text.Json.Serialization;

using Gym.Domain.Notifications;

namespace Gym.Application.Notifications.Broadcasts;

/// <summary>
/// One broadcast as its page lists it (BUSINESS_RULES.md §10 <i>The broadcast</i>): what was written,
/// to which group, where it stands, and how its messages went.
/// </summary>
/// <remarks>
/// The counts come from its messages, as «پیامک‌ها» shows them, so the two pages never disagree.
/// </remarks>
/// <param name="Text">The Owner's text, without the name before it or the opt-out line after it.</param>
/// <param name="Pending">Messages written and not yet answered: being sent, or waiting for a retry.</param>
/// <param name="CostToman">What its sent messages cost, in Toman (the provider's Rial ÷ 10).</param>
public sealed record BroadcastResponse(
    Guid Id,
    string Text,
    [property: JsonConverter(typeof(JsonStringEnumConverter<BroadcastAudience>))] BroadcastAudience Audience,
    [property: JsonConverter(typeof(JsonStringEnumConverter<BroadcastStatus>))] BroadcastStatus Status,
    [property: JsonConverter(typeof(JsonStringEnumConverter<BroadcastStopReason>))] BroadcastStopReason? StopReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EndedAt,
    int Sent,
    int Failed,
    int Unknown,
    int Pending,
    decimal CostToman)
{
    public static BroadcastResponse From(SmsBroadcast broadcast, IReadOnlyCollection<BroadcastMessageCount> counts)
    {
        ArgumentNullException.ThrowIfNull(broadcast);
        ArgumentNullException.ThrowIfNull(counts);

        int Count(NotificationStatus status) =>
            counts.Where(count => count.Status == status).Sum(count => count.Messages);

        return new BroadcastResponse(
            broadcast.Id,
            broadcast.Text,
            broadcast.Audience,
            broadcast.Status,
            broadcast.StopReason,
            broadcast.CreatedAt,
            broadcast.EndedAt,
            Count(NotificationStatus.Sent),
            Count(NotificationStatus.Failed),
            Count(NotificationStatus.Unknown),
            Count(NotificationStatus.Pending),
            counts.Sum(count => count.CostRial) / 10m);
    }
}

/// <summary>How many of one broadcast's messages have one status, and what they cost in Rial.</summary>
public sealed record BroadcastMessageCount(Guid BroadcastId, NotificationStatus Status, int Messages, decimal CostRial);
