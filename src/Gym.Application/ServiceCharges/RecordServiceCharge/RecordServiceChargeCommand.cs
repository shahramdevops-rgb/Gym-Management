using System.Text.Json.Serialization;

using Gym.Domain.ServiceCharges;

namespace Gym.Application.ServiceCharges.RecordServiceCharge;

/// <param name="Kind">
/// <see cref="ServiceChargeKind.Cardio"/> (هوازی), <see cref="ServiceChargeKind.Analysis"/>
/// (آنالیز, task 6.5.29) or <see cref="ServiceChargeKind.Other"/> (متفرقه, task 6.5.36): each is a
/// single typed amount, so a new kind was a frontend change rather than a new endpoint.
/// </param>
public sealed record RecordServiceChargeCommand(
    [property: JsonConverter(typeof(JsonStringEnumConverter<ServiceChargeKind>))] ServiceChargeKind Kind,
    decimal Amount);
