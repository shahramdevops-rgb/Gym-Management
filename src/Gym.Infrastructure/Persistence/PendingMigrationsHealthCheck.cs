using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Gym.Infrastructure.Persistence;

/// <summary>
/// Reports a database whose schema is older than the code running against it.
/// </summary>
/// <remarks>
/// <para>
/// Migrations are never applied at startup (docs/ARCHITECTURE.md): a bundle runs during
/// deployment, and locally the developer runs <c>dotnet ef database update</c>. That leaves a
/// gap where the application starts happily against a schema it no longer matches, and the only
/// symptom is an unexplained 500 the first time someone writes to an affected table — a column
/// the code stopped sending is still <c>NOT NULL</c>, a column it started sending does not exist.
/// </para>
/// <para>
/// This turns that into a named, visible state. <c>/health</c> answers 503, the Persian status
/// page shows it, and a deploy that forgot the bundle is caught before anyone uses the front
/// desk rather than after.
/// </para>
/// <para>
/// A database that cannot be reached at all is not this check's problem: <c>AddDbContextCheck</c>
/// already reports that, so an unreachable database here is reported as degraded rather than
/// claimed to be a migration problem.
/// </para>
/// </remarks>
public sealed class PendingMigrationsHealthCheck(AppDbContext db) : IHealthCheck
{
    public const string Name = "migrations";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        IEnumerable<string> pending;
        try
        {
            pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Degraded("Could not read the migration history.", exception);
        }

        var names = pending.ToList();

        return names.Count == 0
            ? HealthCheckResult.Healthy("The database schema matches the application.")
            : HealthCheckResult.Unhealthy(
                $"The database is missing {names.Count} migration(s): {string.Join(", ", names)}. " +
                "Run the migration bundle, or `dotnet ef database update` locally.");
    }
}
