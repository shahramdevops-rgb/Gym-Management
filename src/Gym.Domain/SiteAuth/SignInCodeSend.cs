using Gym.Domain.Common;

namespace Gym.Domain.SiteAuth;

/// <summary>
/// One code SMS that may have cost the gym money: what the limits count, and what the panel will
/// show as the count and cost of code SMS (BUSINESS_RULES.md §15). Never holds the code.
/// </summary>
/// <remarks>
/// Written for a send Kavenegar accepted, and for one that left with no answer (it may have been
/// sent and paid for). A send Kavenegar refused, or that never left, cost nothing and is not here.
/// Only ever added: nothing changes or deletes a row, as with the other records of money spent.
/// </remarks>
public sealed class SignInCodeSend : Entity
{
    // For EF Core.
    private SignInCodeSend()
    {
    }

    /// <summary>E.164, as on <see cref="SignInCode"/>.</summary>
    public string PhoneNumber { get; private set; } = string.Empty;

    public DateTimeOffset SentAt { get; private set; }

    /// <summary>A send again of a code already sent, rather than a new code.</summary>
    public bool IsSendAgain { get; private set; }

    /// <summary>Kavenegar's id; <c>null</c> when no answer came back.</summary>
    public long? ProviderMessageId { get; private set; }

    /// <summary>What Kavenegar charged, in Rial as it reports it; <c>null</c> when no answer came back.</summary>
    public decimal? CostRial { get; private set; }

    public static SignInCodeSend Record(string phoneNumber, DateTimeOffset sentAt, bool isSendAgain, long? providerMessageId, decimal? costRial)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);

        return new SignInCodeSend
        {
            PhoneNumber = phoneNumber,
            SentAt = sentAt,
            IsSendAgain = isSendAgain,
            ProviderMessageId = providerMessageId,
            CostRial = costRial,
        };
    }
}
