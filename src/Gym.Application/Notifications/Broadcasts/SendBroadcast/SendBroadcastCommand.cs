using System.Text.Json.Serialization;

using Gym.Domain.Notifications;

namespace Gym.Application.Notifications.Broadcasts.SendBroadcast;

/// <summary>The Owner confirmed the broadcast (task 10.13): the same text and group the preview was given.</summary>
/// <param name="Text">The Owner's text, without the name or the opt-out line, which the system adds.</param>
public sealed record SendBroadcastCommand(
    string Text,
    [property: JsonConverter(typeof(JsonStringEnumConverter<BroadcastAudience>))] BroadcastAudience Audience);
