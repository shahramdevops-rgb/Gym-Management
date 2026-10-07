using System.Net;
using System.Text;

namespace Gym.Api.IntegrationTests.Notifications;

/// <summary>
/// Stands where Kavenegar would be: the last handler of an <see cref="HttpClient"/>, so no request
/// ever leaves the machine. Answers each request with what the test scripts and keeps every request,
/// with its form body read, for the test to look at.
/// </summary>
internal sealed class StubKavenegar(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer)
    : HttpMessageHandler
{
    public StubKavenegar(Func<HttpRequestMessage, HttpResponseMessage> answer)
        : this((request, _) => Task.FromResult(answer(request)))
    {
    }

    public List<(HttpMethod Method, Uri Uri, string Body)> Requests { get; } = [];

    /// <summary>Kavenegar's own answer: <c>return.status</c> and the same HTTP status.</summary>
    public static HttpResponseMessage Answer(int status, string entries = "null") =>
        Raw((HttpStatusCode)status, $$"""{"return":{"status":{{status}},"message":"پیام"},"entries":{{entries}}}""", "application/json");

    public static HttpResponseMessage Raw(HttpStatusCode status, string body, string mediaType = "text/html") =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) };

    public static HttpClient Client(HttpMessageHandler handler, TimeSpan? timeout = null) => new(handler)
    {
        BaseAddress = Gym.Infrastructure.Sms.KavenegarSmsSender.BaseAddress,
        Timeout = timeout ?? Gym.Infrastructure.Sms.KavenegarSmsSender.Timeout,
    };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.Method, request.RequestUri!, body));

        return await answer(request, cancellationToken);
    }
}
