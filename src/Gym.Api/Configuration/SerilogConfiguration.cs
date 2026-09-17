using System.Diagnostics;
using System.Globalization;

using Serilog;
using Serilog.AspNetCore;
using Serilog.Events;

namespace Gym.Api.Configuration;

/// <summary>
/// Serilog setup. Sinks and levels live in <c>appsettings.json</c> so production can change
/// them without a rebuild; only what configuration cannot express is expressed here.
/// </summary>
public static class SerilogConfiguration
{
    /// <summary>
    /// Installs a logger that works before the host exists.
    /// </summary>
    /// <remarks>
    /// Without this, anything thrown while the host is being built — a missing connection
    /// string, a bad user-secret, the password check in <c>AddInfrastructure</c> — dies with
    /// no log line at all, which is precisely when a log line is worth most. The bootstrap
    /// logger writes to the console and is replaced by the configured one once the host is up.
    /// </remarks>
    public static void UseBootstrapLogger() =>
        Log.Logger = new LoggerConfiguration()
            // Invariant culture, not the machine's. This project runs with
            // InvariantGlobalization disabled so the app can format Persian dates and digits;
            // without an explicit provider here, a machine set to fa-IR would write log
            // timestamps and numbers in Persian digits, which no log query would match.
            // Persian formatting is the frontend's job; logs are for engineers.
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
            .CreateBootstrapLogger();

    public static IHostBuilder UseConfiguredSerilog(this IHostBuilder host)
    {
        ArgumentNullException.ThrowIfNull(host);

        // The three-argument overload reads from configuration and from the service provider,
        // so sinks are configurable and any sink needing DI can resolve it.
        return host.UseSerilog((context, services, logger) => logger
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext());
    }

    /// <summary>
    /// One summary line per request instead of the framework's several, enriched with the
    /// properties worth querying in Seq.
    /// </summary>
    public static void ConfigureRequestLogging(RequestLoggingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

        options.GetLevel = GetLevel;

        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            // Properties, not sentence fragments: Seq can filter on these, which is the whole
            // point of structured logging over string logging.
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("CorrelationId", Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier);
        };
    }

    private static LogEventLevel GetLevel(HttpContext httpContext, double elapsed, Exception? exception)
    {
        if (exception is not null || httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            return LogEventLevel.Error;
        }

        // A health probe polled every ten seconds is 8,640 events a day that say nothing.
        // Logging has a cost, so deciding what not to log is part of setting logging up.
        // Debug rather than off: the events still exist when something is actually wrong.
        if (IsHealthCheck(httpContext))
        {
            return LogEventLevel.Debug;
        }

        return LogEventLevel.Information;
    }

    private static bool IsHealthCheck(HttpContext httpContext) =>
        httpContext.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
}
