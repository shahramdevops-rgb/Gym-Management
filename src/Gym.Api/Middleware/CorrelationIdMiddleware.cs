using System.Diagnostics;

namespace Gym.Api.Middleware;

/// <summary>
/// Returns the current request's trace id to the caller as <c>X-Correlation-Id</c>.
/// </summary>
/// <remarks>
/// <para>
/// The id is not invented here. ASP.NET Core already starts a W3C <see cref="Activity"/> per
/// request and takes its trace id from an inbound <c>traceparent</c> header when there is one,
/// so a call that crossed from the frontend keeps the same id on both sides. Generating a
/// separate correlation id would mean carrying two ids that mean the same thing.
/// </para>
/// <para>
/// This matters because one request produces log events from several layers. Serilog enriches
/// every event with the trace id, so Seq can reassemble them into one story — and because the
/// id travels back in the response, a user reporting "it failed at 14:02" can quote an id that
/// finds the exact request.
/// </para>
/// </remarks>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Activity.Current is null when tracing is switched off; TraceIdentifier is always
        // populated, so it is the fallback rather than a second source of truth.
        var correlationId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        // Registered as a callback rather than set directly: headers cannot be written once
        // the response has started, and an endpoint that throws mid-write would otherwise
        // lose the id on exactly the responses where it is most useful.
        context.Response.OnStarting(state =>
        {
            var response = (HttpResponse)state;
            response.Headers[HeaderName] = correlationId;

            return Task.CompletedTask;
        },
        context.Response);

        return next(context);
    }
}
