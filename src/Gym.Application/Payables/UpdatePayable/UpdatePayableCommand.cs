using System.Text.Json.Serialization;

using Gym.Domain.Payables;

namespace Gym.Application.Payables.UpdatePayable;

/// <param name="Version">The <c>version</c> from the record as it was read before editing.</param>
public sealed record UpdatePayableCommand(
    [property: JsonConverter(typeof(JsonStringEnumConverter<PayableKind>))] PayableKind Kind,
    decimal Amount,
    DateOnly DueDate,
    string Payee,
    string Description,
    Guid CategoryId,
    uint Version,
    int? InstallmentNumber = null,
    int? InstallmentCount = null);
