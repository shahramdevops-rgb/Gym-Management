using System.Text.Json.Serialization;

using Gym.Domain.Payables;

namespace Gym.Application.Payables.ListPayablesDueSoon;

/// <summary>
/// The header's alert (BUSINESS_RULES.md §9 <i>Cheques and instalments</i>): pending cheques and
/// instalments dated within 5 days or already past their date, the earliest first.
/// </summary>
/// <param name="Today">The gym's today, the day <see cref="PayableDueSoonResponse.DaysLeft"/> counts from.</param>
public sealed record PayablesDueSoonResponse(DateOnly Today, IReadOnlyList<PayableDueSoonResponse> Items);

/// <param name="InstallmentNumber">The n of «قسط n از N»; null on a cheque.</param>
/// <param name="InstallmentCount">The N of «قسط n از N»; null on a cheque.</param>
/// <param name="DaysLeft">Days from today to the due date: 0 is today, below 0 is past its date.</param>
public sealed record PayableDueSoonResponse(
    Guid PayableId,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PayableKind>))] PayableKind Kind,
    string Payee,
    decimal Amount,
    DateOnly DueDate,
    int? InstallmentNumber,
    int? InstallmentCount,
    int DaysLeft);
