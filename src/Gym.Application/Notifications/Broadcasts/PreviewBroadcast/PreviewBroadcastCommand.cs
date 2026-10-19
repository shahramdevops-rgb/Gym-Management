using System.Text.Json.Serialization;

using Gym.Domain.Notifications;

namespace Gym.Application.Notifications.Broadcasts.PreviewBroadcast;

/// <summary>The broadcast as the Owner has written it, before the confirmation (task 10.13).</summary>
/// <param name="Text">The Owner's text, without the name or the opt-out line, which the system adds.</param>
public sealed record PreviewBroadcastCommand(
    string Text,
    [property: JsonConverter(typeof(JsonStringEnumConverter<BroadcastAudience>))] BroadcastAudience Audience);
