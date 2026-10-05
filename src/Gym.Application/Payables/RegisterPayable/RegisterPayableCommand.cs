using System.Text.Json.Serialization;

using Gym.Domain.Payables;

namespace Gym.Application.Payables.RegisterPayable;

/// <param name="DueDate">The date on the cheque or the instalment's due day. Any date: an old one can be entered late.</param>
/// <param name="Payee">در وجه on a cheque; the bank or the seller for an instalment.</param>
/// <param name="CategoryId">The expense category its payment is recorded under.</param>
/// <param name="InstallmentNumber">The n of «قسط n از N»: required for an instalment, null for a cheque.</param>
/// <param name="InstallmentCount">The N of «قسط n از N»: required for an instalment, null for a cheque.</param>
public sealed record RegisterPayableCommand(
    [property: JsonConverter(typeof(JsonStringEnumConverter<PayableKind>))] PayableKind Kind,
    decimal Amount,
    DateOnly DueDate,
    string Payee,
    string Description,
    Guid CategoryId,
    int? InstallmentNumber = null,
    int? InstallmentCount = null);
