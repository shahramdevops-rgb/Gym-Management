using Gym.Application.Common.Sms;

using Microsoft.Extensions.Logging;

namespace Gym.Infrastructure.Sms;

/// <summary>
/// An <see cref="ISmsSender"/> that sends nothing: it logs the message and answers "sent", free of
/// charge. Tests always use it, so a test run can never reach the provider or spend the gym's credit.
/// </summary>
/// <remarks>
/// The log names the template and the last four digits of the number, never the values: they hold
/// a member's name, and logs are kept longer and read by more people than the database.
/// </remarks>
public sealed partial class FakeSmsSender(ILogger<FakeSmsSender> logger) : ISmsSender
{
    private long _lastMessageId;

    public Task<SmsSendResult> SendTemplateAsync(SmsTemplateMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var messageId = Interlocked.Increment(ref _lastMessageId);
        var maskedReceptor = Mask(message.Receptor);
        LogFakeSend(logger, message.Template, maskedReceptor, messageId);

        return Task.FromResult(SmsSendResult.Sent(messageId, costRial: 0m));
    }

    /// <summary><c>+989121234567</c> becomes <c>…4567</c>: enough to tell two test numbers apart.</summary>
    private static string Mask(string receptor) =>
        receptor.Length <= 4 ? receptor : string.Concat("…", receptor.AsSpan(receptor.Length - 4));

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "SMS not sent (fake sender): template {Template} to {Receptor}, fake message id {MessageId}")]
    private static partial void LogFakeSend(ILogger logger, string template, string receptor, long messageId);
}
