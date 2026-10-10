using System.Collections.Concurrent;
using System.Data.Common;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Gym.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Counts the SQL commands EF Core sends while serving one request, so a test can prove that a
/// list costs the same number of queries for 1 row as for 25 (the "N+1" problem, task 11.3).
/// </summary>
/// <remarks>
/// <para>
/// Only requests that carry <see cref="HeaderName"/> are counted, under the header's value. The
/// host runs Hangfire's server and its own background work, which query the same database at
/// moments no test controls; keying by request keeps those out of the count.
/// </para>
/// <para>
/// The request is found through <see cref="IHttpContextAccessor"/>, which follows the request's
/// async flow, so it answers inside the interceptor as it does inside a handler.
/// </para>
/// </remarks>
internal sealed class QueryCounter(IHttpContextAccessor httpContextAccessor) : DbCommandInterceptor
{
    public const string HeaderName = "X-Test-Query-Count";

    private readonly ConcurrentDictionary<string, int> _counts = new(StringComparer.Ordinal);

    /// <summary>The commands counted under <paramref name="key"/>; zero when none ran.</summary>
    public int CountFor(string key) => _counts.GetValueOrDefault(key);

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Count();
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Count();
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Count();
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        Count();
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Count();
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Count();
        return ValueTask.FromResult(result);
    }

    private void Count()
    {
        var key = httpContextAccessor.HttpContext?.Request.Headers[HeaderName].ToString();
        if (!string.IsNullOrEmpty(key))
        {
            _counts.AddOrUpdate(key, 1, (_, count) => count + 1);
        }
    }
}
