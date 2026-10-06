using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common.Sms;
using Gym.Domain.Notifications;
using Gym.Infrastructure.Sms;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gym.Api.IntegrationTests.Notifications;

/// <summary>
/// Which sender the app uses, and what the fake one answers (BUSINESS_RULES.md §10 <i>Sending</i>):
/// tests must never reach the provider.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SmsSenderTests(DatabaseFixture fixture)
{
    private static readonly SmsTemplateMessage Message =
        new("+989121234567", "gymExpiring", new SmsTokens("۱۴۰۵/۰۷/۲۰", Token10: "سارا محمدی"));

    [Fact]
    public void SmsSender_InTheTestHost_IsTheFakeOne()
    {
        fixture.Services.GetRequiredService<ISmsSender>().ShouldBeOfType<FakeSmsSender>();
    }

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
    public void SmsOptionsValidator_FakeProvider_Succeeds()
    {
        new SmsOptionsValidator().Validate(null, Valid()).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void SmsOptions_NothingConfigured_DefaultsToTheFakeProvider()
    {
        // A missing setting must never send a message somebody pays for.
        new SmsOptions().Provider.ShouldBe(SmsProviders.Fake);
    }

    [Theory]
    [InlineData("Kavenegar")]
    [InlineData("fake")]
    [InlineData("")]
    public void SmsOptionsValidator_UnknownProvider_Fails(string provider)
    {
        var options = Valid();
        options.Provider = provider;

        var result = new SmsOptionsValidator().Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Sms:Provider");
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

        var result = new SmsOptionsValidator().Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Sms:MaxAttempts");
    }

    [Fact]
    public void SmsOptionsValidator_WaitsNotOneFewerThanTries_Fails()
    {
        var options = Valid();
        options.RetryDelays = [TimeSpan.FromMinutes(1)];

        var result = new SmsOptionsValidator().Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Sms:RetryDelays");
    }

    [Fact]
    public void SmsOptionsValidator_NegativeWait_Fails()
    {
        var options = Valid();
        options.RetryDelays = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(-5)];

        new SmsOptionsValidator().Validate(null, options).Failed.ShouldBeTrue();
    }

    private static SmsOptions Valid() => new()
    {
        Provider = SmsProviders.Fake,
        MaxAttempts = 3,
        RetryDelays = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)],
    };
}
