namespace Gym.Application.Cheques.UpdateCheque;

/// <param name="Version">The <c>version</c> from the cheque as it was read before editing.</param>
public sealed record UpdateChequeCommand(
    decimal Amount,
    DateOnly DueDate,
    string Payee,
    string Description,
    uint Version);
