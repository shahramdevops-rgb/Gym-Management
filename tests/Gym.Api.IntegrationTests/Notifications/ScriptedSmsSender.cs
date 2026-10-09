using Gym.Application.Common.Sms;

namespace Gym.Api.IntegrationTests.Notifications;

/// <summary>
/// Answers each request with the next scripted result, then "sent" once the script runs out, and
/// keeps every request it was given. Never reaches a provider.
/// </summary>
internal sealed class ScriptedSmsSender(params SmsSendResult[] script) : ISmsSender
{
    public const decimal Cost = 1_350m;

    private readonly Queue<SmsSendResult> _script = new(script);
    private long _lastMessageId;

    public List<SmsMessage> Requests { get; } = [];

    public Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        Requests.Add(message);

        return Task.FromResult(_script.Count > 0
            ? _script.Dequeue()
            : SmsSendResult.Sent(Interlocked.Increment(ref _lastMessageId), Cost));
    }
}
