using System.Text;
using System.Text.Json;

using Gym.Api.Common;
using Gym.Api.Configuration;
using Gym.Api.Middleware;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Middleware;

/// <summary>
/// What a caller sees when something the code did not expect goes wrong: a 500 in the same
/// ProblemDetails shape as every other error, an id that finds the real exception in Seq, and
/// nothing at all about what actually broke.
/// </summary>
public sealed class GlobalExceptionHandlerTests
{
    private const string SecretMessage = "Password=super-secret; column members.phone does not exist";

    private static async Task<(int StatusCode, string Body)> HandleAsync(Exception exception)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApiProblemDetails();

        await using var provider = services.BuildServiceProvider();

        var handler = new GlobalExceptionHandler(provider.GetRequiredService<IProblemDetailsService>());

        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/members";

        using var body = new MemoryStream();
        context.Response.Body = body;

        var handled = await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        // Returning false would let the exception fall through to the default handler and an
        // empty 500, so the contract of this handler starts with "I handled it".
        handled.ShouldBeTrue();

        return (context.Response.StatusCode, Encoding.UTF8.GetString(body.ToArray()));
    }

    [Fact]
    public async Task TryHandleAsync_WhenExceptionEscapes_WritesProblemDetailsWith500()
    {
        var (statusCode, body) = await HandleAsync(new InvalidOperationException(SecretMessage));
        var problem = JsonDocument.Parse(body).RootElement;

        statusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        problem.GetProperty("status").GetInt32().ShouldBe(StatusCodes.Status500InternalServerError);
        problem.GetProperty(ProblemDetailsFields.Code).GetString().ShouldBe(ApiErrorCodes.Unexpected);
    }

    [Fact]
    public async Task TryHandleAsync_WhenExceptionEscapes_LeaksNothingAboutTheCause()
    {
        var exception = new InvalidOperationException(SecretMessage, new IOException("inner detail"));

        var (_, body) = await HandleAsync(exception);

        // An exception message can carry a connection string, a file path or a member's name,
        // and it helps the reader with nothing. The correlation id below is the way back to it.
        body.ShouldNotContain("super-secret");
        body.ShouldNotContain("inner detail");
        body.ShouldNotContain(nameof(InvalidOperationException));
        body.ShouldNotContain("stackTrace", Case.Insensitive);
    }

    [Fact]
    public async Task TryHandleAsync_WhenExceptionEscapes_IncludesTheCorrelationId()
    {
        var (_, body) = await HandleAsync(new InvalidOperationException(SecretMessage));

        JsonDocument.Parse(body).RootElement
            .GetProperty(ProblemDetailsFields.CorrelationId)
            .GetString()
            .ShouldNotBeNullOrWhiteSpace();
    }
}
