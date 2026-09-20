using Gym.Application.Common;
using Gym.Application.Common.Security;
using Gym.Application.Staff;
using Gym.Infrastructure.Attendances;
using Gym.Infrastructure.Calendar;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Jobs;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Phones;
using Gym.Infrastructure.Persistence.Interceptors;
using Gym.Infrastructure.Subscriptions;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Gym.Infrastructure;

/// <summary>
/// The one entry point Gym.Api uses to wire up this layer. Keeping the registrations here
/// rather than in <c>Program.cs</c> means the composition root names a layer, not a provider.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = BuildConnectionString(configuration);

        // TryAdd, because task 0.4 registers TimeProvider in Program.cs as well. The rule in
        // CLAUDE.md is that nothing calls DateTime.UtcNow, so the clock has to be injectable
        // and the tests can substitute FakeTimeProvider.
        services.TryAddSingleton(TimeProvider.System);

        // Stateless and depend only on singletons (ICurrentUser reads the current request
        // through an accessor), so one instance of each serves every context.
        services.AddSingleton<AuditableEntityInterceptor>();
        services.AddSingleton<AuditLogInterceptor>();

        services.AddDbContext<AppDbContext>((provider, options) => options
            .UseNpgsql(connectionString)
            // PascalCase CLR names to snake_case database names, so C# reads like C# and SQL
            // reads like SQL. It also removes the need to quote identifiers by hand in
            // Postgres, which folds unquoted names to lower case.
            .UseSnakeCaseNamingConvention()
            // Order matters: timestamps are stamped first, then the audit log reads the entries.
            .AddInterceptors(
                provider.GetRequiredService<AuditableEntityInterceptor>(),
                provider.GetRequiredService<AuditLogInterceptor>()));

        // Application asks for the interface; it resolves to the same scoped instance the
        // framework already tracks, so both views share one change tracker and one transaction.
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        // AddIdentityCore, not AddIdentity: AddIdentity also registers cookie authentication and
        // SignInManager. This API authenticates with JWT bearer tokens, and UserAuthenticator
        // does its password and lockout checks with UserManager alone.
        services.AddIdentityCore<User>(options =>
            {
                // BUSINESS_RULES.md §1: 5 consecutive wrong passwords lock the account for
                // 15 minutes. AllowedForNewUsers sets LockoutEnabled on every account created
                // from now on, the Owner included.
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;

                // BUSINESS_RULES.md §1: at least 8 characters, a letter and a digit, no case
                // or symbol requirement — passwords are typed on a Persian keyboard at the
                // front desk. The built-in per-class checks are switched off in favor of
                // LetterAndDigitPasswordValidator, which checks letter/digit without regard
                // to case.
                options.User.AllowedUserNameCharacters = UserNamePolicy.AllowedCharacters;
                options.Password.RequiredLength = PasswordPolicy.MinimumLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddPasswordValidator<LetterAndDigitPasswordValidator>();

        services.AddScoped<IUserAuthenticator, UserAuthenticator>();
        services.AddScoped<IStaffAccounts, StaffAccounts>();

        // "Today" in the gym's time zone. A misspelled time zone stops the app at startup instead
        // of silently producing the wrong day.
        services.AddOptions<GymCalendarOptions>()
            .Bind(configuration.GetSection(GymCalendarOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<GymCalendarOptions>, GymCalendarOptionsValidator>();
        services.AddSingleton<IGymCalendar, GymCalendar>();

        // Gym:MaxFreezeDaysPerSubscription (BUSINESS_RULES.md §4 Freeze). Same section as the
        // calendar's time zone, a separate Options type because it is a different concern.
        services.AddOptions<SubscriptionPolicyOptions>()
            .Bind(configuration.GetSection(SubscriptionPolicyOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<SubscriptionPolicyOptions>, SubscriptionPolicyOptionsValidator>();
        services.AddSingleton<ISubscriptionPolicy, SubscriptionPolicy>();

        // Gym:CancelCheckInWindowMinutes (BUSINESS_RULES.md §7 Cancel check-in). Same section,
        // its own Options type for the same reason as the subscription policy above.
        services.AddOptions<AttendancePolicyOptions>()
            .Bind(configuration.GetSection(AttendancePolicyOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<AttendancePolicyOptions>, AttendancePolicyOptionsValidator>();
        services.AddSingleton<IAttendancePolicy, AttendancePolicy>();

        // Stateless: libphonenumber's metadata is loaded once and shared.
        services.AddOptions<PhoneOptions>().Bind(configuration.GetSection(PhoneOptions.SectionName));
        services.AddSingleton<IPhoneNormalizer, LibPhoneNumberNormalizer>();

        // Bound from the section passed in rather than with BindConfiguration, which needs
        // IConfiguration in the container. The section is a live view, so values added to the
        // configuration later (user-secrets, a test host's settings) are still picked up.
        // ValidateOnStart turns a missing signing key into a startup failure, not a failed login.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        // Without this, /health would only report that the process is running, and an
        // orchestrator would happily route traffic to an API that cannot reach its database.
        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database");

        // The nightly auto-checkout job (BUSINESS_RULES.md §7). Scheduled once the host is
        // built, by Gym.Api.Program calling RecurringJobScheduler.ScheduleRecurringJobs.
        services.AddHangfireJobs(connectionString);

        return services;
    }

    /// <summary>
    /// Recombines the committed half of the connection string with the secret half. The
    /// password is a separate configuration key because a configuration value cannot have
    /// child keys, so it cannot be layered onto <c>ConnectionStrings:Postgres</c> directly.
    /// Locally it comes from user-secrets, in production from an environment variable.
    /// </summary>
    private static string BuildConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Postgres is not configured.");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        var password = configuration["Postgres:Password"];
        if (!string.IsNullOrWhiteSpace(password))
        {
            builder.Password = password;
        }

        // Fail at startup with an actionable message rather than at the first query with a
        // bare authentication error, the same reasoning as ${VAR:?...} in docker-compose.yml.
        if (string.IsNullOrWhiteSpace(builder.Password))
        {
            throw new InvalidOperationException(
                "No database password was configured. Locally, run: dotnet user-secrets set " +
                "\"Postgres:Password\" \"<password from .env>\" --project src/Gym.Api. " +
                "In production, supply Postgres__Password or a full ConnectionStrings__Postgres.");
        }

        return builder.ConnectionString;
    }
}
