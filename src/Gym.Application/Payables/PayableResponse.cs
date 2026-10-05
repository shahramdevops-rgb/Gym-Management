using System.Text.Json.Serialization;

using Gym.Domain.Payables;

namespace Gym.Application.Payables;

/// <param name="CategoryName">The expense category's current name: nothing copies it.</param>
/// <param name="InstallmentNumber">The n of «قسط n از N»; null on a cheque.</param>
/// <param name="InstallmentCount">The N of «قسط n از N»; null on a cheque.</param>
/// <param name="ExpenseId">The standing expense its payment recorded; null unless it is paid.</param>
/// <param name="Version">Sent back with an edit, so a stale edit is refused.</param>
public sealed record PayableResponse(
    Guid Id,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PayableKind>))] PayableKind Kind,
    decimal Amount,
    DateOnly DueDate,
    string Payee,
    string Description,
    Guid CategoryId,
    string CategoryName,
    int? InstallmentNumber,
    int? InstallmentCount,
    PayableStatus Status,
    Guid RegisteredByUserId,
    DateTimeOffset? PaidAt,
    Guid? PaidByUserId,
    Guid? ExpenseId,
    DateTimeOffset? CancelledAt,
    string? CancelReason,
    Guid? CancelledByUserId,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt)
{
    public static PayableResponse From(Payable payable, string categoryName, Guid? expenseId)
    {
        ArgumentNullException.ThrowIfNull(payable);
        ArgumentNullException.ThrowIfNull(categoryName);

        return new PayableResponse(
            payable.Id,
            payable.Kind,
            payable.Amount,
            payable.DueDate,
            payable.Payee,
            payable.Description,
            payable.CategoryId,
            categoryName,
            payable.InstallmentNumber,
            payable.InstallmentCount,
            payable.GetStatus(),
            payable.RegisteredByUserId,
            payable.PaidAt,
            payable.PaidByUserId,
            expenseId,
            payable.CancelledAt,
            payable.CancelReason,
            payable.CancelledByUserId,
            payable.Version,
            payable.CreatedAt,
            payable.UpdatedAt);
    }
}
