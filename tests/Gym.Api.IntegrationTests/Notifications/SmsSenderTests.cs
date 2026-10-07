using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common.Sms;
using Gym.Domain.Notifications;
using Gym.Infrastructure.Sms;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gym.Api.IntegrationTests.Notifications;

/// <summary>
/// Which sender the app uses, and what the fake one answers (BUSINESS_RULES.md §10 <i>Sending</i> and
/// <i>Development and tests</i>): tests must never reach the provider, and outside Production the real
/// provider only reaches the numbers in <c>Sms:AllowedReceptors</c>.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SmsSenderTests(DatabaseFixture fixture)
{
    private const string ApiKey = "secret-kavenegar-key";
    private const string Listed = "+989121234567";
    private const string NotListed = "+989359876543";

    private static readonly SmsTemplateMessage Message = To(Listed);

    // ---- The test host ----

    [Fact]
    public async Task SmsSender_InTheTestHost_IsTheFakeOne()
    {
        await using var scope = fixture.CreateScope();

        scope.ServiceProvider.GetRequiredService<ISmsSender>().ShouldBeOfType<FakeSmsSender>();
        scope.ServiceProvider.GetRequiredService<ISmsAccount>().ShouldBeOfType<FakeSmsSender>();
    }

    // ---- The fake sender ----

    [Fact]
    public async Task SendTemplateAsync_FakeSender_AnswersSentFreeOfCharge()
    {
        var sender = new FakeSmsSender(NullLogger<FakeSmsSender>.Instance);

        var result = await sender.SendTemplateAsync(Message, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(SmsSendOutcome.Sent);
        result.ProviderMessageId.ShouldNotBeNull();
        result.CostRial.ShouldBe(0m);
        result.ErrorCode.ShouldBeNull();
    }

    [Fact]
    public async Task SendTemplateAsync_FakeSenderTwice_GivesEachMessageItsOwnId()
    {
        var sender = new FakeSmsSender(NullLogger<FakeSmsSender>.Instance);

        var first = await sender.SendTemplateAsync(Message, TestContext.Current.CancellationToken);
        var second = await sender.SendTemplateAsync(Message, TestContext.Current.CancellationToken);

        second.ProviderMessageId.ShouldNotBe(first.ProviderMessageId);
    }

    [Fact]
    public async Task FakeAccount_KnowsNoDeliveryAndIsInTestMode()
    {
        var account = new FakeSmsSender(NullLogger<FakeSmsSender>.Instance);

        (await account.GetDeliveriesAsync([1, 2], TestContext.Current.CancellationToken)).ShouldBeEmpty();
        (await account.GetCreditAsync(TestContext.Current.CancellationToken)).ShouldBe(SmsCredit.TestMode);
    }

    // ---- Which sender (task 10.4) ----

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void AddSms_FakeProvider_IsTheFakeOneInEveryEnvironment(string environment)
    {
        using var app = Build(environment, SmsProviders.Fake);

        app.Sender.ShouldBeOfType<FakeSmsSender>();
        app.Account.ShouldBeOfType<FakeSmsSender>();
    }

    [Fact]
    public void AddSms_KavenegarInProduction_IsKavenegarWithNoList()
    {
        using var app = Build("Production", SmsProviders.Kavenegar);

        app.Sender.ShouldBeOfType<KavenegarSmsSender>();
        app.Account.ShouldBeOfType<KavenegarSmsSender>();
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    public void AddSms_KavenegarOutsideProduction_GoesThroughTheList(string environment)
    {
        using var app = Build(environment, SmsProviders.Kavenegar);

        app.Sender.ShouldBeOfType<AllowListSmsSender>();
        app.Account.ShouldBeOfType<KavenegarSmsSender>();
    }

    [Fact]
    public async Task SendTemplateAsync_KavenegarOutsideProductionWithNoList_SendsNothing()
    {
        // The developer's database holds made-up numbers: forgetting the list must never reach anyone.
        using var app = Build("Development", SmsProviders.Kavenegar);

        var result = await app.Sender.SendTemplateAsync(Message, TestContext.Current.CancellationToken);

        app.Kavenegar.Requests.ShouldBeEmpty();
        result.Outcome.ShouldBe(SmsSendOutcome.Sent);
        result.CostRial.ShouldBe(0m);
    }

    [Fact]
    public async Task SendTemplateAsync_KavenegarOutsideProduction_ReachesOnlyTheListedNumbers()
    {
        using var app = Build("Development", SmsProviders.Kavenegar, allowed: [Listed]);

        var listed = await app.Sender.SendTemplateAsync(To(Listed), TestContext.Current.CancellationToken);
        var notListed = await app.Sender.SendTemplateAsync(To(NotListed), TestContext.Current.CancellationToken);

        Fields(app.Kavenegar.Requests.ShouldHaveSingleItem().Body).ShouldContain("receptor=09121234567");
        listed.ProviderMessageId.ShouldBe(StubMessageId);
        notListed.CostRial.ShouldBe(0m);
    }

    [Fact]
    public async Task KavenegarAccount_OutsideProductionWithNoList_StillAsks()
    {
        // Asking sends nothing and reaches nobody, so the list does not hold it back (§10).
        using var app = Build("Development", SmsProviders.Kavenegar);

        await app.Account.GetCreditAsync(TestContext.Current.CancellationToken);

        app.Kavenegar.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Kavenegar_EveryRequest_NeverWritesTheApiKeyToTheLogs()
    {
        // The key is in the address of every request; the HTTP client's own loggers would log it.
        using var app = Build("Production", SmsProviders.Kavenegar);

        await app.Sender.SendTemplateAsync(Message, TestContext.Current.CancellationToken);
        await app.Account.GetDeliveriesAsync([1], TestContext.Current.CancellationToken);
        await app.Account.GetCreditAsync(TestContext.Current.CancellationToken);

        app.Kavenegar.Requests.Count.ShouldBe(3);
        app.Kavenegar.Requests.ShouldAllBe(request => request.Uri.AbsolutePath.Contains(ApiKey));
        app.Logs.ShouldNotBeEmpty();
        app.Logs.ShouldAllBe(line => !line.Contains(ApiKey));
    }

    // ---- Options ----

    [Fact]
    public void SmsOptions_NothingConfigured_DefaultsToTheFakeProviderWithNoNumbers()
    {
        // A missing setting must never send a message somebody pays for.
        var options = new SmsOptions();

        options.Provider.ShouldBe(SmsProviders.Fake);
        options.AllowedReceptors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(SmsProviders.Fake)]
    [InlineData(SmsProviders.Kavenegar)]
    public void SmsOptionsValidator_KnownProvider_Succeeds(string provider)
    {
        var options = Valid();
        options.Provider = provider;

        Validate(options).Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("kavenegar")]
    [InlineData("fake")]
    [InlineData("")]
    public void SmsOptionsValidator_UnknownProvider_Fails(string provider)
    {
        var options = Valid();
        options.Provider = provider;

        var result = Validate(options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Sms:Provider");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void SmsOptionsValidator_KavenegarWithNoKey_Fails(string key)
    {
        var options = Valid();
        options.Provider = SmsProviders.Kavenegar;
        options.Kavenegar.ApiKey = key;

        var result = Validate(options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Sms:Kavenegar:ApiKey");
    }

    [Fact]
    public void SmsOptionsValidator_FakeWithNoKey_Succeeds()
    {
        var options = Valid();
        options.Kavenegar.ApiKey = string.Empty;

        Validate(options).Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("09121234567")]
    [InlineData("+98 912 123 4567")]
    [InlineData("+982112345678")]
    [InlineData("+98912123456")]
    public void SmsOptionsValidator_ListedNumberNotAsMembersAreStored_Fails(string receptor)
    {
        var options = Valid();
        options.AllowedReceptors = [Listed, receptor];

        var result = Validate(options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Sms:AllowedReceptors");
    }

    [Fact]
    public void SmsOptionsValidator_ListInDevelopment_Succeeds()
    {
        var options = Valid();
        options.AllowedReceptors = [Listed];

        Validate(options, "Development").Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void SmsOptionsValidator_ListInProduction_Fails()
    {
        // §10: the server has no such list; one left there would quietly keep members' messages back.
        var options = Valid();
        options.AllowedReceptors = [Listed];

        var result = Validate(options, "Production");

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Production");
    }

    // ---- Retries (task 10.3) ----

    [Fact]
    public void SmsOptions_AppSettings_ThreeTriesWaitingOneThenFiveMinutes()
    {
        // BUSINESS_RULES.md §0, as appsettings.json sets it and the run receives it.
        var schedule = fixture.Services.GetRequiredService<SmsRetrySchedule>();

        schedule.MaxAttempts.ShouldBe(3);
        schedule.Delays.ShouldBe([TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void SmsOptionsValidator_MaxAttemptsOutOfRange_Fails(int maxAttempts)
    {
        var options = Valid();
        options.MaxAttempts = maxAttempts;

        var result = Validate(options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Sms:MaxAttempts");
    }

    [Fact]
    public void SmsOptionsValidator_WaitsNotOneFewerThanTries_Fails()
    {
        var options = Valid();
        options.RetryDelays = [TimeSpan.FromMinutes(1)];

        var result = Validate(options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Sms:RetryDelays");
    }

    [Fact]
    public void SmsOptionsValidator_NegativeWait_Fails()
    {
        var options = Valid();
        options.RetryDelays = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(-5)];

        Validate(options).Failed.ShouldBeTrue();
    }

    // ---- Helpers ----

    private const long StubMessageId = 8792343;

    private static SmsTemplateMessage To(string receptor) =>
        new(receptor, "gymExpiring", new SmsTokens("۱۴۰۵/۰۷/۲۰", Token10: "سارا محمدی"));

    private static SmsOptions Valid() => new()
    {
        Provider = SmsProviders.Fake,
        MaxAttempts = 3,
        RetryDelays = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)],
        Kavenegar = { ApiKey = ApiKey },
    };

    private static Microsoft.Extensions.Options.ValidateOptionsResult Validate(SmsOptions options, string environment = "Development") =>
        new SmsOptionsValidator(new HostingEnvironment { EnvironmentName = environment }).Validate(null, options);

    private static string Fields(string body) => Uri.UnescapeDataString(body);

    /// <summary>
    /// The services exactly as <see cref="SmsRegistration.AddSms"/> builds them, with Kavenegar replaced by
    /// <see cref="StubKavenegar"/> at the very end of the HTTP client, and every log line kept.
    /// </summary>
    private static BuiltSms Build(string environment, string smsProvider, string[]? allowed = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Sms:Provider"] = smsProvider,
            ["Sms:MaxAttempts"] = "3",
            ["Sms:RetryDelays:0"] = "00:01:00",
            ["Sms:RetryDelays:1"] = "00:05:00",
            ["Sms:Kavenegar:ApiKey"] = ApiKey,
        };
        for (var i = 0; i < (allowed ?? []).Length; i++)
        {
            settings[$"Sms:AllowedReceptors:{i}"] = allowed![i];
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var logs = new RecordingLoggerProvider();
        // The delivery question is refused, so the sender itself writes a line the key could be in.
        var kavenegar = new StubKavenegar(request => request.RequestUri!.AbsolutePath switch
        {
            var path when path.EndsWith("/verify/lookup.json", StringComparison.Ordinal) =>
                StubKavenegar.Answer(200, $$"""[{"messageid":{{StubMessageId}},"cost":1350}]"""),
            var path when path.EndsWith("/sms/status.json", StringComparison.Ordinal) => StubKavenegar.Answer(409),
            _ => StubKavenegar.Answer(200, """{"remaincredit":1250000}"""),
        });

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
        services.AddSingleton<IHostEnvironment>(new HostingEnvironment { EnvironmentName = environment });
        services.AddSms(configuration);
        services.AddHttpClient<KavenegarSmsSender>().ConfigurePrimaryHttpMessageHandler(() => kavenegar);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        var scope = provider.CreateScope();

        return new BuiltSms(provider, scope, kavenegar, logs.Lines);
    }

    private sealed class BuiltSms(ServiceProvider provider, IServiceScope scope, StubKavenegar kavenegar, List<string> logs) : IDisposable
    {
        public ISmsSender Sender => scope.ServiceProvider.GetRequiredService<ISmsSender>();

        public ISmsAccount Account => scope.ServiceProvider.GetRequiredService<ISmsAccount>();

        public StubKavenegar Kavenegar => kavenegar;

        public List<string> Logs => logs;

        public void Dispose()
        {
            scope.Dispose();
            provider.Dispose();
        }
    }

    /// <summary>Keeps every log line, from every category, at every level.</summary>
    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public List<string> Lines { get; } = [];

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, Lines);

        public void Dispose()
        {
        }

        private sealed class RecordingLogger(string category, List<string> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (lines)
                {
                    lines.Add($"{category}: {formatter(state, exception)} {state}");
                }
            }
        }
    }
}
