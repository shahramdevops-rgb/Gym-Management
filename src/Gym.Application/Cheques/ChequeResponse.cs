using Gym.Domain.Cheques;

namespace Gym.Application.Cheques;

/// <param name="Version">Sent back with an edit, so a stale edit is refused.</param>
public sealed record ChequeResponse(
    Guid Id,
    decimal Amount,
    DateOnly DueDate,
    string Payee,
    string Description,
    ChequeStatus Status,
    Guid RegisteredByUserId,
    DateTimeOffset? PassedAt,
    Guid? PassedByUserId,
    DateTimeOffset? CancelledAt,
    string? CancelReason,
    Guid? CancelledByUserId,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt)
{
    public static ChequeResponse From(Cheque cheque)
    {
        ArgumentNullException.ThrowIfNull(cheque);

        return new ChequeResponse(
            cheque.Id,
            cheque.Amount,
            cheque.DueDate,
            cheque.Payee,
            cheque.Description,
            cheque.GetStatus(),
            cheque.RegisteredByUserId,
            cheque.PassedAt,
            cheque.PassedByUserId,
            cheque.CancelledAt,
            cheque.CancelReason,
            cheque.CancelledByUserId,
            cheque.Version,
            cheque.CreatedAt,
            cheque.UpdatedAt);
    }
}
