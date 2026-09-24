using Microsoft.AspNetCore.HttpOverrides;

namespace Gym.Api.Configuration;

/// <summary>
/// Makes the API see the real client behind the reverse proxy (Caddy in production).
/// </summary>
/// <remarks>
/// <para>
/// Without this, every request arrives from the proxy's address, so the per-IP login rate limit
/// puts every user in one bucket and the audit log records the proxy as the source of every
/// action. On an internet-facing host that is a defect, not a cosmetic one (ADR 0003).
/// </para>
/// <para>
/// The headers are honoured only when the request comes from <c>ForwardedHeaders:TrustedNetwork</c>,
/// the Compose network Caddy lives on. Anyone else who sends <c>X-Forwarded-For</c> is ignored,
/// because otherwise a client could invent an address per request and never hit the limit.
/// With the key unset, which is development and the tests, the middleware is not added at all.
/// </para>
/// </remarks>
public static class ForwardedHeadersConfiguration
{
    public const string TrustedNetworkKey = "ForwardedHeaders:TrustedNetwork";

    public static IServiceCollection AddApiForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var trustedNetwork = configuration[TrustedNetworkKey];
        if (string.IsNullOrWhiteSpace(trustedNetwork))
        {
            return services;
        }

        // Parsed here, at startup, so a typo in the setting stops the API instead of silently
        // trusting nobody (every user in one bucket again) or, worse, everybody.
        if (!System.Net.IPNetwork.TryParse(trustedNetwork, out var network))
        {
            throw new InvalidOperationException(
                $"{TrustedNetworkKey} is not a network in CIDR form (for example 172.28.0.0/24): \"{trustedNetwork}\".");
        }

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // The defaults trust loopback. Nothing here should be trusted except what is named.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            options.KnownIPNetworks.Add(network);

            // One proxy, so one hop: the address Caddy saw is the client.
            options.ForwardLimit = 1;
        });

        return services;
    }

    /// <summary>Adds the middleware if, and only if, <see cref="AddApiForwardedHeaders"/> configured it.</summary>
    public static WebApplication UseApiForwardedHeaders(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!string.IsNullOrWhiteSpace(app.Configuration[TrustedNetworkKey]))
        {
            app.UseForwardedHeaders();
        }

        return app;
    }
}
