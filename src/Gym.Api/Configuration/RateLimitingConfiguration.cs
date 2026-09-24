using System.Globalization;
using System.Threading.RateLimiting;

using Gym.Api.Common;

using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Gym.Api.Configuration;

/// <summary>The <c>RateLimiting:Login</c> configuration section.</summary>
public sealed class LoginRateLimitOptions
{
    public const string SectionName = "RateLimiting:Login";

    /// <summary>Attempts allowed per IP address per <see cref="Window"/>.</summary>
    public int PermitLimit { get; set; } = 10;

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// Per-IP rate limiting for login (BUSINESS_RULES.md §1: 10 attempts per minute per IP).
/// </summary>
/// <remarks>
/// <para>
/// Lockout protects one account; this protects the endpoint. Without it, an attacker could
/// try one password against every user name and never trigger any single account's lockout.
/// </para>
/// <para>
/// The limit is configuration rather than a constant only so the integration tests, which log
/// in many times a minute from one address, can raise it. The default is the business rule.
/// </para>
/// </remarks>
public static class RateLimitingConfiguration
{
    public const string LoginPolicy = "login";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<LoginRateLimitOptions>()
            .Bind(configuration.GetSection(LoginRateLimitOptions.SectionName));

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(LoginPolicy, httpContext =>
            {
                var limits = httpContext.RequestServices.GetRequiredService<IOptions<LoginRateLimitOptions>>().Value;

                // One bucket per client address. Behind a reverse proxy the address is the real
                // client's only because ForwardedHeadersConfiguration rewrote it; see the gotcha
                // in docs/ARCHITECTURE.md.
                var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.PermitLimit,
                    Window = limits.Window,

                    // Reject at once instead of queueing: a login that waits a minute to be
                    // processed helps nobody, and a queue is memory an attacker gets to fill.
                    QueueLimit = 0,
                });
            });

            options.OnRejected = WriteTooManyRequestsAsync;
        });

        return services;
    }

    /// <summary>
    /// The same ProblemDetails shape as every other failure, with the correlation id added by
    /// <see cref="ProblemDetailsConfiguration"/>, so the frontend can map the code to Persian.
    /// </summary>
    private static async ValueTask WriteTooManyRequestsAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        var problemDetails = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();

        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests.",
                Detail = "Too many attempts from this address. Try again later.",
                Extensions = { [ProblemDetailsFields.Code] = ApiErrorCodes.TooManyRequests },
            },
        });
    }
}
