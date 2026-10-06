namespace Gym.Application.Common.Sms;

/// <summary>
/// How one request to the provider went, sorted the way BUSINESS_RULES.md §10 <i>Sending</i> sorts
/// it, so the caller decides what to do from the outcome alone and never from a provider's code.
/// </summary>
public enum SmsSendOutcome
{
    /// <summary>Accepted: <see cref="SmsSendResult.ProviderMessageId"/> and the cost are set.</summary>
    Sent,

    /// <summary>A failure that may pass (provider busy, network down before the request left): try again later.</summary>
    RetryableFailure,

    /// <summary>A failure that will not pass (template not approved, invalid number…): <c>Failed</c> at once.</summary>
    PermanentFailure,

    /// <summary>The account's credit is used up: this one fails and the rest of the run is not sent.</summary>
    CreditExhausted,

    /// <summary>The request left and no answer came back: it may have been sent. Never retried by itself.</summary>
    Unknown,
}

/// <param name="ErrorCode">The provider's own code for a failure, kept with the message; <c>null</c> when none came back.</param>
/// <param name="CostRial">What the provider charged, in Rial as it reports it.</param>
public sealed record SmsSendResult(
    SmsSendOutcome Outcome,
    long? ProviderMessageId = null,
    decimal? CostRial = null,
    int? ErrorCode = null)
{
    public static SmsSendResult Sent(long providerMessageId, decimal costRial) =>
        new(SmsSendOutcome.Sent, providerMessageId, costRial);
}
