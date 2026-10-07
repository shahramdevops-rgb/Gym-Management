using System.Net;

using Gym.Application.Common.Sms;
using Gym.Domain.Notifications;
using Gym.Infrastructure.Sms;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Gym.Api.IntegrationTests.Notifications;

/// <summary>
/// How every answer from Kavenegar becomes an outcome (BUSINESS_RULES.md §10 <i>Sending</i>, task
/// 10.4). Kavenegar is <see cref="StubKavenegar"/>: nothing here reaches the network.
/// </summary>
public sealed class KavenegarSmsSenderTests
{
    private const string ApiKey = "test-api-key";

    private static readonly SmsTemplateMessage Message =
        new("+989121234567", "gymExpiring", new SmsTokens("۱۴۰۵/۰۷/۲۰", Token10: "سارا محمدی"));

    // ---- Sending ----

    [Fact]
    public async Task SendTemplateAsync_Accepted_IsSentWithKavenegarsIdAndCost()
    {
        var stub = new StubKavenegar(_ => StubKavenegar.Answer(200, """[{"messageid":8792343,"status":5,"cost":1350}]"""));

        var result = await SendAsync(stub);

        result.Outcome.ShouldBe(SmsSendOutcome.Sent);
        result.ProviderMessageId.ShouldBe(8792343);
        result.CostRial.ShouldBe(1350m);
        result.ErrorCode.ShouldBeNull();
    }

    [Fact]
    public async Task SendTemplateAsync_Request_PostsTheTemplateAndItsValuesToLookupWithALocalNumber()
    {
        var stub = new StubKavenegar(_ => StubKavenegar.Answer(200, """[{"messageid":1,"cost":0}]"""));

        await SendAsync(stub);

        var request = stub.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri.AbsolutePath.ShouldBe($"/v1/{ApiKey}/verify/lookup.json");
        var fields = Fields(request.Body);
        fields["receptor"].ShouldBe("09121234567");
        fields["template"].ShouldBe("gymExpiring");
        fields["token"].ShouldBe("۱۴۰۵/۰۷/۲۰");
        fields["token10"].ShouldBe("سارا محمدی");
        fields.Keys.ShouldNotContain("token2");
        fields.Keys.ShouldNotContain("token20");
    }

    [Theory]
    [InlineData(409, SmsSendOutcome.RetryableFailure)]
    [InlineData(418, SmsSendOutcome.CreditExhausted)]
    [InlineData(411, SmsSendOutcome.PermanentFailure)]
    [InlineData(422, SmsSendOutcome.PermanentFailure)]
    [InlineData(424, SmsSendOutcome.PermanentFailure)]
    [InlineData(426, SmsSendOutcome.PermanentFailure)]
    [InlineData(431, SmsSendOutcome.PermanentFailure)]
    [InlineData(403, SmsSendOutcome.PermanentFailure)]
    [InlineData(501, SmsSendOutcome.PermanentFailure)]
    public async Task SendTemplateAsync_KavenegarsCode_BecomesItsOutcomeAndIsKept(int code, SmsSendOutcome outcome)
    {
        var result = await SendAsync(new StubKavenegar(_ => StubKavenegar.Answer(code)));

        result.Outcome.ShouldBe(outcome);
        result.ErrorCode.ShouldBe(code);
        result.ProviderMessageId.ShouldBeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway, "<html>502 Bad Gateway</html>")]
    [InlineData(HttpStatusCode.GatewayTimeout, "")]
    [InlineData(HttpStatusCode.OK, "<html>ok</html>")]
    [InlineData(HttpStatusCode.OK, "{}")]
    public async Task SendTemplateAsync_AnAnswerThatIsNotKavenegars_IsUnknown(HttpStatusCode status, string body)
    {
        // The request left: it may have been sent and paid for, so it must never be tried again by itself.
        var result = await SendAsync(new StubKavenegar(_ => StubKavenegar.Raw(status, body)));

        result.Outcome.ShouldBe(SmsSendOutcome.Unknown);
    }

    [Fact]
    public async Task SendTemplateAsync_AcceptedWithNoMessage_IsUnknown()
    {
        var result = await SendAsync(new StubKavenegar(_ => StubKavenegar.Answer(200, "[]")));

        result.Outcome.ShouldBe(SmsSendOutcome.Unknown);
    }

    [Theory]
    [InlineData(HttpRequestError.NameResolutionError)]
    [InlineData(HttpRequestError.ConnectionError)]
    [InlineData(HttpRequestError.SecureConnectionError)]
    public async Task SendTemplateAsync_NoConnection_MayBeTriedAgain(HttpRequestError error)
    {
        var result = await SendAsync(new StubKavenegar(_ => throw new HttpRequestException(error, "no connection")));

        result.Outcome.ShouldBe(SmsSendOutcome.RetryableFailure);
        result.ErrorCode.ShouldBeNull();
    }

    [Theory]
    [InlineData(HttpRequestError.ResponseEnded)]
    [InlineData(HttpRequestError.InvalidResponse)]
    [InlineData(HttpRequestError.Unknown)]
    public async Task SendTemplateAsync_ConnectionLostAfterTheRequestLeft_IsUnknown(HttpRequestError error)
    {
        var result = await SendAsync(new StubKavenegar(_ => throw new HttpRequestException(error, "dropped")));

        result.Outcome.ShouldBe(SmsSendOutcome.Unknown);
    }

    [Fact]
    public async Task SendTemplateAsync_NoAnswerInTime_IsUnknown()
    {
        var stub = new StubKavenegar(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Never reached.");
        });
        using var client = StubKavenegar.Client(stub, TimeSpan.FromMilliseconds(50));

        var result = await Sender(client).SendTemplateAsync(Message, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(SmsSendOutcome.Unknown);
    }

    // ---- Delivery ----

    [Fact]
    public async Task GetDeliveriesAsync_KavenegarsStatuses_BecomeDeliveriesAndTheRestAreLeftOut()
    {
        var stub = new StubKavenegar(_ => StubKavenegar.Answer(200, """
            [{"messageid":1,"status":10},{"messageid":2,"status":11},{"messageid":3,"status":14},
             {"messageid":4,"status":6},{"messageid":5,"status":13},{"messageid":6,"status":1},
             {"messageid":7,"status":4},{"messageid":8,"status":5},{"messageid":9,"status":100}]
            """));
        using var client = StubKavenegar.Client(stub);

        var deliveries = await Sender(client).GetDeliveriesAsync([1, 2, 3, 4, 5, 6, 7, 8, 9], TestContext.Current.CancellationToken);

        deliveries.ShouldBe(new Dictionary<long, SmsDelivery>
        {
            [1] = SmsDelivery.Delivered,
            [2] = SmsDelivery.NotDelivered,
            [3] = SmsDelivery.BlockedByReceiver,
            [4] = SmsDelivery.NotDelivered,
            [5] = SmsDelivery.NotDelivered,
        });
        var request = stub.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Get);
        request.Uri.AbsolutePath.ShouldBe($"/v1/{ApiKey}/sms/status.json");
        Uri.UnescapeDataString(request.Uri.Query).ShouldBe("?messageid=1,2,3,4,5,6,7,8,9");
    }

    [Fact]
    public async Task GetDeliveriesAsync_ManyMessages_AsksInBatches()
    {
        var stub = new StubKavenegar(_ => StubKavenegar.Answer(200, "[]"));
        using var client = StubKavenegar.Client(stub);
        var ids = Enumerable.Range(1, (KavenegarSmsSender.StatusBatchSize * 2) + 1).Select(id => (long)id).ToList();

        await Sender(client).GetDeliveriesAsync(ids, TestContext.Current.CancellationToken);

        stub.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task GetDeliveriesAsync_KavenegarRefuses_KnowsNothing()
    {
        using var client = StubKavenegar.Client(new StubKavenegar(_ => StubKavenegar.Answer(409)));

        var deliveries = await Sender(client).GetDeliveriesAsync([1, 2], TestContext.Current.CancellationToken);

        deliveries.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetDeliveriesAsync_NotReached_KnowsNothing()
    {
        using var client = StubKavenegar.Client(new StubKavenegar(_ =>
            throw new HttpRequestException(HttpRequestError.ConnectionError, "no connection")));

        var deliveries = await Sender(client).GetDeliveriesAsync([1], TestContext.Current.CancellationToken);

        deliveries.ShouldBeEmpty();
    }

    // ---- Credit ----

    [Fact]
    public async Task GetCreditAsync_Answer_IsTheRemainingCreditInRial()
    {
        var stub = new StubKavenegar(_ => StubKavenegar.Answer(200, """{"remaincredit":1250000,"expiredate":1856619709,"type":"master"}"""));
        using var client = StubKavenegar.Client(stub);

        var credit = await Sender(client).GetCreditAsync(TestContext.Current.CancellationToken);

        credit.ShouldBe(new SmsCredit(IsTestMode: false, RemainingRial: 1_250_000m));
        stub.Requests.ShouldHaveSingleItem().Uri.AbsolutePath.ShouldBe($"/v1/{ApiKey}/account/info.json");
    }

    [Theory]
    [InlineData(403)]
    [InlineData(502)]
    public async Task GetCreditAsync_NoUsableAnswer_IsUnavailable(int status)
    {
        using var client = StubKavenegar.Client(new StubKavenegar(_ => status == 502
            ? StubKavenegar.Raw(HttpStatusCode.BadGateway, "<html></html>")
            : StubKavenegar.Answer(status)));

        var credit = await Sender(client).GetCreditAsync(TestContext.Current.CancellationToken);

        credit.ShouldBe(SmsCredit.Unavailable);
    }

    // ---- Helpers ----

    private static async Task<SmsSendResult> SendAsync(StubKavenegar stub)
    {
        using var client = StubKavenegar.Client(stub);

        return await Sender(client).SendTemplateAsync(Message, TestContext.Current.CancellationToken);
    }

    private static KavenegarSmsSender Sender(HttpClient client) => new(
        client,
        Options.Create(new SmsOptions { Provider = SmsProviders.Kavenegar, Kavenegar = { ApiKey = ApiKey } }),
        NullLogger<KavenegarSmsSender>.Instance);

    private static Dictionary<string, string> Fields(string body) => body
        .Split('&')
        .Select(pair => pair.Split('='))
        .ToDictionary(
            pair => Uri.UnescapeDataString(pair[0]),
            pair => Uri.UnescapeDataString(pair[1].Replace('+', ' ')));
}
