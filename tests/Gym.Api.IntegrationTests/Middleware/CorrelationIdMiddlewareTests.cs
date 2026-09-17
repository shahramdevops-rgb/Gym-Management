using System.Diagnostics;

using Gym.Api.Middleware;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Gym.Api.IntegrationTests.Middleware;

/// <summary>
/// The correlation id is what turns a dozen scattered log events back into one story, so it
/// has to survive the cases where it matters most: a request that already carried an id, and
/// a request that blew up.
/// </summary>
public sealed class CorrelationIdMiddlewareTests : IDisposable
{
    private readonly Activity _activity;

    public CorrelationIdMiddlewareTests()
    {
        _activity = new Activity("test-request");
        _activity.SetIdFormat(ActivityIdFormat.W3C);
        _activity.Start();
    }

    public void Dispose()
    {
        _activity.Stop();
        _activity.Dispose();
    }

    private static (HttpContext Context, RecordingResponseFeature Response) CreateContext()
    {
        var response = new RecordingResponseFeature();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(response);

        return (context, response);
    }

    [Fact]
    public async Task InvokeAsync_WhenRequestSucceeds_ReturnsTheActivityTraceId()
    {
        var (context, response) = CreateContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);
        await response.StartResponseAsync();

        // The id is not invented here: a call that arrived with a W3C traceparent keeps the
        // same id on both sides, so the frontend's log line and the API's line join up.
        response.Headers[CorrelationIdMiddleware.HeaderName]
            .ToString()
            .ShouldBe(_activity.TraceId.ToString());
    }

    [Fact]
    public async Task InvokeAsync_WhenRequestSucceeds_CallsTheRestOfThePipeline()
    {
        var (context, _) = CreateContext();
        var called = false;
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            called = true;

            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        called.ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WhenNoActivityIsRunning_FallsBackToTheTraceIdentifier()
    {
        // Tracing can be switched off entirely; the response must still carry an id.
        _activity.Stop();
        Activity.Current = null;

        var (context, response) = CreateContext();
        context.TraceIdentifier = "fallback-id";
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);
        await response.StartResponseAsync();

        response.Headers[CorrelationIdMiddleware.HeaderName].ToString().ShouldBe("fallback-id");
    }

    [Fact]
    public async Task InvokeAsync_WhenThePipelineThrows_StillSetsTheHeader()
    {
        var (context, response) = CreateContext();
        var middleware = new CorrelationIdMiddleware(_ => throw new InvalidOperationException("boom"));

        await Should.ThrowAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
        await response.StartResponseAsync();

        // The failing request is exactly the one somebody will ask you to look up, which is
        // why the callback is registered before the pipeline runs rather than after it returns.
        response.Headers[CorrelationIdMiddleware.HeaderName]
            .ToString()
            .ShouldBe(_activity.TraceId.ToString());
    }

    [Fact]
    public async Task InvokeAsync_WhenCalled_DoesNotWriteTheHeaderBeforeTheResponseStarts()
    {
        var (context, response) = CreateContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        // Headers cannot be written once the response has started, so the value is deferred to
        // an OnStarting callback rather than set eagerly.
        response.Headers.ContainsKey(CorrelationIdMiddleware.HeaderName).ShouldBeFalse();
    }
}
