using System.Globalization;
using System.Threading.RateLimiting;

using Gym.Api.Common;

using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Gym.Api.Configuration;

/// <summary>A per-IP limit: so many requests per <see cref="Window"/> from one client address.</summary>
public abstract class PerAddressRateLimitOptions
{
    /// <summary>Requests allowed per IP address per <see cref="Window"/>.</summary>
    public abstract int PermitLimit { get; set; }

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>The <c>RateLimiting:Login</c> configuration section.</summary>
public sealed class LoginRateLimitOptions : PerAddressRateLimitOptions
{
    public const string SectionName = "RateLimiting:Login";

    public override int PermitLimit { get; set; } = 10;
}

/// <summary>The <c>RateLimiting:Session</c> configuration section: refresh and logout together.</summary>
public sealed class SessionRateLimitOptions : PerAddressRateLimitOptions
{
    public const string SectionName = "RateLimiting:Session";

    public override int PermitLimit { get; set; } = 60;
}

/// <summary>
/// The <c>RateLimiting:SiteCode</c> configuration section: asking the site for a one-time code
/// (BUSINESS_RULES.md §15). Generous, because Iranian mobile carriers put many phones behind one
/// address; the per-phone and per-day limits are the ones that protect the SMS credit.
/// </summary>
public sealed class SiteCodeRateLimitOptions : PerAddressRateLimitOptions
{
    public const string SectionName = "RateLimiting:SiteCode";

    public override int PermitLimit { get; set; } = 20;

    public SiteCodeRateLimitOptions() => Window = TimeSpan.FromMinutes(10);
}

/// <summary>
/// The <c>RateLimiting:SiteSignIn</c> configuration section: typing a code and signing up on the
/// site (BUSINESS_RULES.md §15), 30 per 10 minutes per IP. Each code has its own 5 tries; this
/// stops one address from spreading its guesses over many phones.
/// </summary>
public sealed class SiteSignInRateLimitOptions : PerAddressRateLimitOptions
{
    public const string SectionName = "RateLimiting:SiteSignIn";

    public override int PermitLimit { get; set; } = 30;

    public SiteSignInRateLimitOptions() => Window = TimeSpan.FromMinutes(10);
}

/// <summary>
/// The <c>RateLimiting:SiteSession</c> configuration section: the site's refresh and logout, 60 a
/// minute per IP, as the staff's are, in a bucket of their own.
/// </summary>
public sealed class SiteSessionRateLimitOptions : PerAddressRateLimitOptions
{
    public const string SectionName = "RateLimiting:SiteSession";

    public override int PermitLimit { get; set; } = 60;
}

/// <summary>
/// Per-IP rate limiting for the endpoints a stranger can reach (BUSINESS_RULES.md §1): 10 login
/// attempts per minute, and 60 refreshes and logouts per minute; and the site's code requests, 20
/// per 10 minutes, its sign-ins and sign-ups, 30 per 10 minutes, and its refreshes and logouts, 60 a
/// minute (§15), each in a bucket of its own so the site's traffic never uses up the staff's.
/// </summary>
/// <remarks>
/// <para>
/// Lockout protects one account; the login limit protects the endpoint. Without it, an attacker
/// could try one password against every user name and never trigger any single account's lockout.
/// </para>
/// <para>
/// Refresh and logout check no password, so they do not share login's small budget, but they are
/// anonymous: without a limit, anyone could send them as fast as the network allows and each one
/// would reach the database (task 11.2). They share one bucket because they are one policy.
/// </para>
/// <para>
/// The limits are configuration rather than constants only so the integration tests, which call
/// these endpoints many times a minute from one address, can raise them. The defaults are the
/// business rule.
/// </para>
/// </remarks>
public static class RateLimitingConfiguration
{
    public const string LoginPolicy = "login";

    public const string SessionPolicy = "session";

    public const string SiteCodePolicy = "site-code";

    public const string SiteSignInPolicy = "site-sign-in";

    public const string SiteSessionPolicy = "site-session";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<LoginRateLimitOptions>()
            .Bind(configuration.GetSection(LoginRateLimitOptions.SectionName));
        services.AddOptions<SessionRateLimitOptions>()
            .Bind(configuration.GetSection(SessionRateLimitOptions.SectionName));
        services.AddOptions<SiteCodeRateLimitOptions>()
            .Bind(configuration.GetSection(SiteCodeRateLimitOptions.SectionName));
        services.AddOptions<SiteSignInRateLimitOptions>()
            .Bind(configuration.GetSection(SiteSignInRateLimitOptions.SectionName));
        services.AddOptions<SiteSessionRateLimitOptions>()
            .Bind(configuration.GetSection(SiteSessionRateLimitOptions.SectionName));

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(LoginPolicy, PerAddress<LoginRateLimitOptions>);
            options.AddPolicy(SessionPolicy, PerAddress<SessionRateLimitOptions>);
            options.AddPolicy(SiteCodePolicy, PerAddress<SiteCodeRateLimitOptions>);
            options.AddPolicy(SiteSignInPolicy, PerAddress<SiteSignInRateLimitOptions>);
            options.AddPolicy(SiteSessionPolicy, PerAddress<SiteSessionRateLimitOptions>);

            options.OnRejected = WriteTooManyRequestsAsync;
        });

        return services;
    }

    private static RateLimitPartition<string> PerAddress<TOptions>(HttpContext httpContext)
        where TOptions : PerAddressRateLimitOptions
    {
        var limits = httpContext.RequestServices.GetRequiredService<IOptions<TOptions>>().Value;

        // One bucket per client address. Behind a reverse proxy the address is the real client's
        // only because ForwardedHeadersConfiguration rewrote it; see the gotcha in
        // docs/ARCHITECTURE.md.
        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limits.PermitLimit,
            Window = limits.Window,

            // Reject at once instead of queueing: a request that waits a minute to be processed
            // helps nobody, and a queue is memory an attacker gets to fill.
            QueueLimit = 0,
        });
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
