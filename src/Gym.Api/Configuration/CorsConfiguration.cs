namespace Gym.Api.Configuration;

/// <summary>
/// CORS for the Vite dev server only.
/// </summary>
/// <remarks>
/// In production the React build is served by Caddy from the same origin as the API
/// (task 6.1), so no CORS policy applies at all. This exists purely because
/// <c>npm run dev</c> serves the frontend from a different port during development.
/// </remarks>
public static class CorsConfiguration
{
    public const string PolicyName = "VitePolicy";

    private const string OriginsKey = "Cors:AllowedOrigins";

    public static IServiceCollection AddVitePolicy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var origins = configuration.GetSection(OriginsKey).Get<string[]>() ?? [];

        // Fail at startup rather than let every browser request die with an opaque CORS error
        // that looks like a frontend bug.
        if (origins.Length is 0)
        {
            throw new InvalidOperationException(
                $"'{OriginsKey}' is empty. Development needs the Vite dev server origin, " +
                "for example http://localhost:5173.");
        }

        return services.AddCors(options => options.AddPolicy(
            PolicyName,
            policy => policy
                .WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                // Phase 1 sends the refresh token as an HttpOnly cookie, which the browser
                // will not attach cross-origin without this. It is also why the origins are
                // listed explicitly: AllowAnyOrigin and AllowCredentials are illegal together,
                // and for good reason — that pair would let any site call this API as the
                // logged-in user.
                .AllowCredentials()
                // Lets the frontend read the id back off a failed response so it can be shown
                // in an error message or attached to a bug report.
                .WithExposedHeaders(Middleware.CorrelationIdMiddleware.HeaderName)));
    }
}
