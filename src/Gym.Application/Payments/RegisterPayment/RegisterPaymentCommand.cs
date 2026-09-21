using System.Text.Json.Serialization;

using Gym.Domain.Payments;

namespace Gym.Application.Payments.RegisterPayment;

public sealed record RegisterPaymentCommand(
    decimal Amount,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentMethod>))] PaymentMethod Method,
    string? ReferenceNumber);
