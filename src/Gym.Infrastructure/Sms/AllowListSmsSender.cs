using Gym.Application.Common.Sms;

using Microsoft.Extensions.Logging;

namespace Gym.Infrastructure.Sms;

/// <summary>
/// Outside Production, the real provider only reaches the numbers in <c>Sms:AllowedReceptors</c>;
/// every other message goes to <see cref="FakeSmsSender"/>, which only logs it (BUSINESS_RULES.md §10
/// <i>Development and tests</i>). The site's one-time codes the same way (§15).
/// </summary>
/// <remarks>
/// The developer's database holds made-up numbers, and a made-up number may belong to a real person
/// who knows nothing of this gym. So the guard is not something to remember to turn on: it is there
/// whenever the environment is not Production, and an empty list keeps every message back.
/// </remarks>
public sealed partial class AllowListSmsSender(
    ISmsSender real,
    IVerificationCodeSender realCodes,
    FakeSmsSender fake,
    IReadOnlyCollection<string> allowedReceptors,
    ILogger<AllowListSmsSender> logger) : ISmsSender, IVerificationCodeSender
{
    public Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (allowedReceptors.Contains(message.Receptor))
        {
            return real.SendAsync(message, cancellationToken);
        }

        var maskedReceptor = FakeSmsSender.Mask(message.Receptor);
        LogKeptBack(logger, maskedReceptor);

        return fake.SendAsync(message, cancellationToken);
    }

    public Task<SmsSendResult> SendCodeAsync(VerificationCodeMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (allowedReceptors.Contains(message.Receptor))
        {
            return realCodes.SendCodeAsync(message, cancellationToken);
        }

        var maskedReceptor = FakeSmsSender.Mask(message.Receptor);
        LogKeptBack(logger, maskedReceptor);

        return fake.SendCodeAsync(message, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SMS to {Receptor} kept back: not in Sms:AllowedReceptors")]
    private static partial void LogKeptBack(ILogger logger, string receptor);
}
