using Gym.Application.Common.Sms;
using Gym.Domain.Notifications;

namespace Gym.Api.IntegrationTests.SiteAuth;

/// <summary>
/// Answers each code with the next scripted result, then "sent" once the script runs out, and keeps
/// every code it was given. Never reaches a provider.
/// </summary>
internal sealed class RecordingCodeSender(params SmsSendResult[] script) : IVerificationCodeSender
{
    public const decimal Cost = 1_150m;

    private readonly Queue<SmsSendResult> _script = new(script);
    private long _lastMessageId;

    public List<VerificationCodeMessage> Requests { get; } = [];

    public Task<SmsSendResult> SendCodeAsync(VerificationCodeMessage message, CancellationToken cancellationToken)
    {
        Requests.Add(message);

        return Task.FromResult(_script.Count > 0
            ? _script.Dequeue()
            : SmsSendResult.Sent(Interlocked.Increment(ref _lastMessageId), Cost));
    }
}

/// <summary>An account that knows the deliveries the test gives it, and records what it was asked.</summary>
internal sealed class ScriptedSmsAccount : ISmsAccount
{
    public Dictionary<long, SmsDelivery> Deliveries { get; } = [];

    public List<long> AskedAbout { get; } = [];

    public Task<IReadOnlyDictionary<long, SmsDelivery>> GetDeliveriesAsync(
        IReadOnlyCollection<long> providerMessageIds, CancellationToken cancellationToken)
    {
        AskedAbout.AddRange(providerMessageIds);

        IReadOnlyDictionary<long, SmsDelivery> known = providerMessageIds
            .Where(Deliveries.ContainsKey)
            .ToDictionary(id => id, id => Deliveries[id]);

        return Task.FromResult(known);
    }

    public Task<SmsCredit> GetCreditAsync(CancellationToken cancellationToken) => Task.FromResult(SmsCredit.TestMode);
}
