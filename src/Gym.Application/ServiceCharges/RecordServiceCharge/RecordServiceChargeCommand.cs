using System.Text.Json.Serialization;

using Gym.Domain.ServiceCharges;

namespace Gym.Application.ServiceCharges.RecordServiceCharge;

/// <param name="Kind">
/// <see cref="ServiceChargeKind.Cardio"/> (هوازی) or <see cref="ServiceChargeKind.Analysis"/>
/// (آنالیز, task 6.5.29): both are a single typed amount, so a second kind was a frontend change
/// rather than a second endpoint.
/// </param>
public sealed record RecordServiceChargeCommand(
    [property: JsonConverter(typeof(JsonStringEnumConverter<ServiceChargeKind>))] ServiceChargeKind Kind,
    decimal Amount);
