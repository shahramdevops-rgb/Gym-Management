using Gym.Application.Common.Sms;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gym.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real <c>Program.cs</c> in process, pointed at a throwaway database.
/// </summary>
/// <remarks>
/// <para>
/// The connection string is handed over as an <i>environment variable</i>, not through
/// <c>ConfigureAppConfiguration</c>. With minimal hosting, <c>Program.cs</c> calls
/// <c>AddInfrastructure(builder.Configuration)</c> before <c>builder.Build()</c>, so the
/// registrations have already read configuration by the time a factory's configuration delta
/// is applied — the override arrives too late and the app starts with no connection string at
/// all. The environment is already in place when <c>CreateBuilder</c> runs, and it is exactly
/// how production supplies this value (see <c>AddInfrastructure</c>'s error message), so the
/// test travels the real path instead of a test-only hook.
/// </para>
/// <para>
/// The environment is <c>Testing</c>, not <c>Development</c>: the Scalar UI and the permissive
/// CORS policy are development-only conveniences, and a suite that ran with them switched on
/// would not be exercising the pipeline that gets deployed.
/// </para>
/// <para>
/// Both variables are process-wide, which is safe here because one fixture owns one factory
/// for the whole run; they are cleared again on disposal.
/// </para>
/// </remarks>
internal sealed class GymApiFactory : WebApplicationFactory<Program>
{
    private const string ConnectionStringVariable = "ConnectionStrings__Postgres";
    private const string EnvironmentVariable = "ASPNETCORE_ENVIRONMENT";
    private const string TestingEnvironment = "Testing";

    // A test-only key: it signs tokens for a throwaway database and is worthless anywhere else.
    private const string SigningKeyVariable = "Jwt__SigningKey";
    private const string TestSigningKey = "integration-tests-signing-key-0123456789abcdef";

    // The real limit is 10 logins per minute per IP, and every test client shares one address,
    // so the suite would trip it within seconds. The test that proves the limiter works builds
    // its own host with a low limit (see LoginRateLimitTests).
    private const string LoginPermitLimitVariable = "RateLimiting__Login__PermitLimit";
    private const string TestLoginPermitLimit = "100000";

    // The same for refresh and logout (60 a minute per IP): see SessionRateLimitTests.
    private const string SessionPermitLimitVariable = "RateLimiting__Session__PermitLimit";
    private const string TestSessionPermitLimit = "100000";

    // Tests always use the fake sender (BUSINESS_RULES.md §10), whatever a developer has set for
    // their own machine: a test run must never reach the SMS provider or spend its credit.
    private const string SmsProviderVariable = "Sms__Provider";
    private const string TestSmsProvider = "Fake";

    public GymApiFactory(string connectionString)
    {
        // The Testcontainers string already carries the password, so the separate
        // Postgres:Password key that AddInfrastructure looks for stays unset.
        Environment.SetEnvironmentVariable(ConnectionStringVariable, connectionString);
        Environment.SetEnvironmentVariable(EnvironmentVariable, TestingEnvironment);
        Environment.SetEnvironmentVariable(SigningKeyVariable, TestSigningKey);
        Environment.SetEnvironmentVariable(LoginPermitLimitVariable, TestLoginPermitLimit);
        Environment.SetEnvironmentVariable(SessionPermitLimitVariable, TestSessionPermitLimit);
        Environment.SetEnvironmentVariable(SmsProviderVariable, TestSmsProvider);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(TestingEnvironment);

        // No real SMS run is ever scheduled from a test: see RecordingSmsRunSchedule.
        builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Singleton<ISmsRunSchedule, RecordingSmsRunSchedule>()));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        Environment.SetEnvironmentVariable(ConnectionStringVariable, null);
        Environment.SetEnvironmentVariable(EnvironmentVariable, null);
        Environment.SetEnvironmentVariable(SigningKeyVariable, null);
        Environment.SetEnvironmentVariable(LoginPermitLimitVariable, null);
        Environment.SetEnvironmentVariable(SessionPermitLimitVariable, null);
        Environment.SetEnvironmentVariable(SmsProviderVariable, null);
    }
}
