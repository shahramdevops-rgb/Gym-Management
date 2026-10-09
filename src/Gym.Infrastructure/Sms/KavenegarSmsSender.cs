using System.Net.Http.Json;
using System.Text.Json;

using Gym.Application.Common.Sms;
using Gym.Domain.Notifications;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gym.Infrastructure.Sms;

/// <summary>
/// Kavenegar's REST API (BUSINESS_RULES.md §10 <i>Sending</i>): <c>sms/send</c> to send the text from
/// the gym's dedicated line (<c>Sms:Kavenegar:Sender</c>), <c>sms/status</c> for delivery,
/// <c>account/info</c> for the credit.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every answer becomes an outcome; nothing throws for a failed send.</b> Kavenegar answers with
/// its own code in <c>return.status</c> (the HTTP status is the same number): 200 sent, 409 busy and
/// 451 too many requests from this IP (both may pass), 418 credit used up, anything else a failure that
/// will not pass, kept with its code.
/// </para>
/// <para>
/// <b>Did the request leave?</b> That decides between a retry and <c>Unknown</c>. No connection at
/// all (the name not found, the connection refused, TLS failing) means nothing reached Kavenegar, so
/// it may be tried again. A request that left and got no usable answer (a timeout, a dropped
/// connection, a proxy's error page, a reply that is not Kavenegar's) may have been sent and paid
/// for: <c>Unknown</c>, never retried by itself, because the request carries no duplicate guard.
/// </para>
/// <para>
/// <b>The API key is in the address of every request</b> (<c>/v1/{key}/sms/send.json</c>).
/// IHttpClientFactory would log that address at Information, so this client's loggers are removed
/// where it is registered, and nothing here ever logs an address.
/// </para>
/// </remarks>
public sealed partial class KavenegarSmsSender(
    HttpClient http,
    IOptions<SmsOptions> options,
    ILogger<KavenegarSmsSender> logger) : ISmsSender, ISmsAccount
{
    public static readonly Uri BaseAddress = new("https://api.kavenegar.com/v1/");

    /// <summary>Long enough for a slow day at Kavenegar; a request with no answer by then is <c>Unknown</c>.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>Message ids asked about in one <c>sms/status</c> request.</summary>
    public const int StatusBatchSize = 100;

    public const int Accepted = 200;
    public const int Busy = 409;
    public const int TooManyRequests = 451;
    public const int CreditUsedUp = SmsProviderCodes.CreditUsedUp;

    private const string SendMethod = "sms/send";
    private const string StatusMethod = "sms/status";
    private const string AccountMethod = "account/info";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly SmsSendResult NoAnswer = new(SmsSendOutcome.Unknown);

    public async Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        using var content = new FormUrlEncodedContent(SendFields(message));

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync(PathOf(SendMethod), content, cancellationToken);
        }
        catch (HttpRequestException exception) when (NeverLeft(exception))
        {
            LogNotReached(logger, SendMethod, exception.HttpRequestError);
            return new SmsSendResult(SmsSendOutcome.RetryableFailure);
        }
        catch (HttpRequestException exception)
        {
            LogNoAnswer(logger, SendMethod, exception.HttpRequestError.ToString());
            return NoAnswer;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout: the request left and the answer did not come back in time.
            LogNoAnswer(logger, SendMethod, "timeout");
            return NoAnswer;
        }

        using (response)
        {
            var answer = await ReadAsync<SendEntry[]>(response, SendMethod, cancellationToken);
            if (answer?.Return is not { } result)
            {
                return NoAnswer;
            }

            switch (result.Status)
            {
                case Accepted when answer.Entries is [var entry, ..]:
                    return SmsSendResult.Sent(entry.MessageId, entry.Cost);

                case Accepted:
                    LogNoAnswer(logger, SendMethod, "accepted with no message");
                    return NoAnswer;

                case Busy or TooManyRequests:
                    LogRefused(logger, SendMethod, result.Status);
                    return new SmsSendResult(SmsSendOutcome.RetryableFailure, ErrorCode: result.Status);

                case CreditUsedUp:
                    LogRefused(logger, SendMethod, result.Status);
                    return new SmsSendResult(SmsSendOutcome.CreditExhausted, ErrorCode: result.Status);

                default:
                    LogRefused(logger, SendMethod, result.Status);
                    return new SmsSendResult(SmsSendOutcome.PermanentFailure, ErrorCode: result.Status);
            }
        }
    }

    public async Task<IReadOnlyDictionary<long, SmsDelivery>> GetDeliveriesAsync(
        IReadOnlyCollection<long> providerMessageIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(providerMessageIds);

        var known = new Dictionary<long, SmsDelivery>();
        foreach (var batch in providerMessageIds.Chunk(StatusBatchSize))
        {
            var path = $"{PathOf(StatusMethod)}?messageid={string.Join(',', batch)}";
            var entries = await AskAsync<StatusEntry[]>(path, StatusMethod, cancellationToken);

            foreach (var entry in entries ?? [])
            {
                if (ToDelivery(entry.Status) is { } delivery)
                {
                    known[entry.MessageId] = delivery;
                }
            }
        }

        return known;
    }

    public async Task<SmsCredit> GetCreditAsync(CancellationToken cancellationToken)
    {
        var account = await AskAsync<AccountEntry>(PathOf(AccountMethod), AccountMethod, cancellationToken);

        return account is null ? SmsCredit.Unavailable : new SmsCredit(IsTestMode: false, account.RemainCredit);
    }

    /// <summary>
    /// Kavenegar's message statuses: 10 delivered, 11 not delivered (phone off or out of reach), 6 the
    /// carrier's error (not delivered), 13 cancelled (cost given back), 14 blocked by the receiver (cost
    /// given back). Queued and sent-to-the-carrier (1, 2, 4, 5) are still on their way, and 100 is an
    /// id it does not know.
    /// </summary>
    internal static SmsDelivery? ToDelivery(int status) => status switch
    {
        10 => SmsDelivery.Delivered,
        11 or 6 => SmsDelivery.NotDelivered,
        13 => SmsDelivery.Cancelled,
        14 => SmsDelivery.BlockedByReceiver,
        _ => null,
    };

    /// <summary>Kavenegar takes a local number (<c>0912…</c>); members' numbers are stored as <c>+98912…</c>.</summary>
    internal static string ToReceptor(string e164) =>
        e164.StartsWith("+98", StringComparison.Ordinal) ? string.Concat("0", e164.AsSpan(3)) : e164;

    /// <summary>Posted as a form, so the Persian text needs no escaping of its own.</summary>
    private Dictionary<string, string> SendFields(SmsMessage message) => new()
    {
        ["receptor"] = ToReceptor(message.Receptor),
        ["sender"] = options.Value.Kavenegar.Sender,
        ["message"] = message.Text,
    };

    /// <summary>No connection was made, so the request cannot have reached Kavenegar.</summary>
    private static bool NeverLeft(HttpRequestException exception) => exception.HttpRequestError
        is HttpRequestError.NameResolutionError
        or HttpRequestError.ConnectionError
        or HttpRequestError.SecureConnectionError
        or HttpRequestError.ProxyTunnelError;

    private string PathOf(string method) => $"{Uri.EscapeDataString(options.Value.Kavenegar.ApiKey)}/{method}.json";

    /// <summary>A question that sends nothing: any failure is logged and comes back as <c>null</c>.</summary>
    private async Task<T?> AskAsync<T>(string path, string method, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var response = await http.GetAsync(path, cancellationToken);
            var answer = await ReadAsync<T>(response, method, cancellationToken);
            if (answer?.Return is not { } result)
            {
                return null;
            }

            if (result.Status != Accepted)
            {
                LogRefused(logger, method, result.Status);
                return null;
            }

            return answer.Entries;
        }
        catch (HttpRequestException exception)
        {
            LogNoAnswer(logger, method, exception.HttpRequestError.ToString());
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogNoAnswer(logger, method, "timeout");
            return null;
        }
    }

    /// <summary>Kavenegar's own answer, or <c>null</c> (logged) when the body is not one.</summary>
    private async Task<Answer<T>?> ReadAsync<T>(HttpResponseMessage response, string method, CancellationToken cancellationToken)
    {
        try
        {
            var answer = await response.Content.ReadFromJsonAsync<Answer<T>>(Json, cancellationToken);
            if (answer?.Return is not null)
            {
                return answer;
            }
        }
        catch (JsonException)
        {
            // A proxy's error page or a broken reply: handled below like any answer that is not Kavenegar's.
        }

        LogNotKavenegar(logger, method, (int)response.StatusCode);

        return null;
    }

    private sealed record Answer<T>(ReturnPart? Return, T? Entries);

    private sealed record ReturnPart(int Status, string? Message);

    private sealed record SendEntry(long MessageId, decimal Cost);

    private sealed record StatusEntry(long MessageId, int Status);

    private sealed record AccountEntry(decimal RemainCredit);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kavenegar not reached for {Method} ({Reason}); nothing left")]
    private static partial void LogNotReached(ILogger logger, string method, HttpRequestError reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kavenegar {Method} left with no answer ({Reason})")]
    private static partial void LogNoAnswer(ILogger logger, string method, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kavenegar refused {Method} with code {Code}")]
    private static partial void LogRefused(ILogger logger, string method, int code);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kavenegar {Method} answered HTTP {HttpStatus} with no answer of its own")]
    private static partial void LogNotKavenegar(ILogger logger, string method, int httpStatus);
}
