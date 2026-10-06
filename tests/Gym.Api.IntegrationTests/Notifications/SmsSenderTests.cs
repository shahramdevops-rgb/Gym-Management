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
        new SmsOptionsValidator().Validate(null, new SmsOptions { Provider = SmsProviders.Fake }).Succeeded.ShouldBeTrue();
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
        var result = new SmsOptionsValidator().Validate(null, new SmsOptions { Provider = provider });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Sms:Provider");
    }
}
