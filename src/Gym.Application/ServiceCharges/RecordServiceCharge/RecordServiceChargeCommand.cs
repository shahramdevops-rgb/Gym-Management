using System.Text.Json.Serialization;

using Gym.Domain.ServiceCharges;

namespace Gym.Application.ServiceCharges.RecordServiceCharge;

/// <param name="Kind">
/// Sent by the caller even though <see cref="ServiceChargeKind.Cardio"/> is the only value today,
/// so a second kind is a frontend change rather than a second endpoint.
/// </param>
public sealed record RecordServiceChargeCommand(
    [property: JsonConverter(typeof(JsonStringEnumConverter<ServiceChargeKind>))] ServiceChargeKind Kind,
    decimal Amount);
