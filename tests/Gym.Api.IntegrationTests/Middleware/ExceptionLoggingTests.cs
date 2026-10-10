using Gym.Api.IntegrationTests.Infrastructure;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Gym.Api.IntegrationTests.Middleware;

/// <summary>
/// A 500's exception reaches the log (task 11.3). Since .NET 10 the exception handler middleware
/// stays silent about an exception that an <c>IExceptionHandler</c> handled, and
/// <c>GlobalExceptionHandler</c> handles every one. Read from the app's own services, so it is
/// Program.cs's setting that is checked.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class ExceptionLoggingTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public void ExceptionHandlerOptions_ExceptionHandledByGlobalHandler_IsStillLogged()
    {
        var options = Fixture.Services.GetRequiredService<IOptions<ExceptionHandlerOptions>>().Value;
        var context = new ExceptionHandlerSuppressDiagnosticsContext
        {
            HttpContext = new DefaultHttpContext(),
            Exception = new InvalidOperationException(),
            ExceptionHandledBy = ExceptionHandledType.ExceptionHandlerService,
        };

        options.SuppressDiagnosticsCallback.ShouldNotBeNull();
        options.SuppressDiagnosticsCallback(context).ShouldBeFalse();
    }
}
