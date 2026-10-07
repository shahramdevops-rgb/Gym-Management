using Gym.Domain.Notifications;

namespace Gym.Application.Common.Sms;

/// <summary>
/// What the provider can tell about the account and the messages already sent
/// (BUSINESS_RULES.md §10 <i>Sending</i>): whether each reached the phone, and the credit left.
/// </summary>
/// <remarks>
/// <para>
/// <b>Apart from <see cref="ISmsSender"/> on purpose.</b> Asking costs nothing and reaches nobody,
/// so it is never held back by <c>Sms:AllowedReceptors</c>, which only guards sending.
/// </para>
/// <para>
/// <b>It never throws for a provider that does not answer.</b> Both answers are information the
/// Owner can do without for a while: no answer comes back as "nothing known", and is logged.
/// </para>
/// </remarks>
public interface ISmsAccount
{
    /// <summary>
    /// What the provider knows about each message's delivery. A message still on its way, or one the
    /// provider no longer knows (after 48 hours), is left out of the answer.
    /// </summary>
    Task<IReadOnlyDictionary<long, SmsDelivery>> GetDeliveriesAsync(
        IReadOnlyCollection<long> providerMessageIds, CancellationToken cancellationToken);

    Task<SmsCredit> GetCreditAsync(CancellationToken cancellationToken);
}

/// <param name="IsTestMode">The fake provider: nothing is really sent, so there is no account to ask.</param>
/// <param name="RemainingRial">
/// The credit left, in Rial as the provider reports it; <c>null</c> in test mode, or when the provider
/// did not answer.
/// </param>
public sealed record SmsCredit(bool IsTestMode, decimal? RemainingRial)
{
    public static SmsCredit TestMode { get; } = new(IsTestMode: true, RemainingRial: null);

    public static SmsCredit Unavailable { get; } = new(IsTestMode: false, RemainingRial: null);
}
