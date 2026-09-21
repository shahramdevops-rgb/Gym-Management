using System.Text.Json.Serialization;

using Gym.Domain.Payments;

namespace Gym.Application.Payments.RegisterRefund;

public sealed record RegisterRefundCommand(
    decimal Amount,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentMethod>))] PaymentMethod Method,
    string? ReferenceNumber,
    string Reason);
