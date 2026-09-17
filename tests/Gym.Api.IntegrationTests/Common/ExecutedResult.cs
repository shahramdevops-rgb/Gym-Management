using System.Text;
using System.Text.Json;

using Gym.Api.Configuration;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Common;

/// <summary>
/// Runs an <see cref="IResult"/> the way the server would and captures what was written.
/// </summary>
/// <remarks>
/// Asserting on the returned object (<c>ShouldBeOfType&lt;ProblemHttpResult&gt;()</c>) would
/// test what the code intends; executing it tests what the caller receives, including the
/// fields added by <see cref="ProblemDetailsConfiguration"/> and the JSON casing. The frontend
/// only ever sees the bytes, so the bytes are what these tests assert on.
/// </remarks>
internal static class ExecutedResult
{
    public static async Task<ExecutedResponse> RunAsync(IResult result, string path = "/api/members")
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApiProblemDetails();

        await using var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = path;

        using var body = new MemoryStream();
        context.Response.Body = body;

        await result.ExecuteAsync(context);

        return new ExecutedResponse(
            context.Response.StatusCode,
            context.Response.ContentType,
            Encoding.UTF8.GetString(body.ToArray()));
    }
}

internal sealed record ExecutedResponse(int StatusCode, string? ContentType, string Body)
{
    public JsonElement Json()
    {
        Body.ShouldNotBeNullOrWhiteSpace("the response was expected to have a JSON body.");

        return JsonDocument.Parse(Body).RootElement.Clone();
    }
}
