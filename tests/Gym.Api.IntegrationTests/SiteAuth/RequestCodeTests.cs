using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common;
using Gym.Application.Common.Security;
using Gym.Application.Common.Sms;
using Gym.Application.SiteAuth.RequestCode;
using Gym.Application.SiteAuth.VerifyCode;
using Gym.Domain.Common;
using Gym.Domain.Members;
using Gym.Domain.Notifications;
using Gym.Domain.SiteAuth;
using Gym.Infrastructure.Calendar;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Gym.Api.IntegrationTests.SiteAuth;

/// <summary>
/// «دریافت کد» and «ارسال دوباره» (BUSINESS_RULES.md §15 <i>The one-time code</i>), against the real
/// database and a clock the test moves. Kavenegar is <see cref="RecordingCodeSender"/>, so every SMS
/// the handler would have paid for is in its list.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class RequestCodeTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string Phone = "+989121234567";

    /// <summary>08:00 UTC is 11:30 in Tehran: the same gym day for the next twelve hours.</summary>
    private static readonly DateTimeOffset Start = new(2026, 10, 26, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(Start);

    private readonly RecordingCodeSender _sender = new();

    private readonly ScriptedSmsAccount _account = new();

    // ---- A new code ----

    [Fact]
    public async Task Handle_NewPhone_SendsACodeValidForSixtyMinutes()
    {
        var response = (await HandleAsync("۰۹۱۲۱۲۳۴۵۶۷")).Value;

        response.Outcome.ShouldBe(RequestCodeOutcome.Sent);
        response.ExpiresAt.ShouldBe(Start.AddMinutes(60));
        response.SendAgainAt.ShouldBe(Start.AddMinutes(2));

        var row = await CodeAsync();
        var message = _sender.Requests.ShouldHaveSingleItem();
        message.Receptor.ShouldBe(Phone);
        message.Code.ShouldBe(CodeFor(row));
        message.Code.Length.ShouldBe(SignInCode.Digits);
        message.MinutesLeft.ShouldBe(60);

        row.LastProviderMessageId.ShouldBe(1);
        var send = (await SendsAsync()).ShouldHaveSingleItem();
        send.PhoneNumber.ShouldBe(Phone);
        send.IsSendAgain.ShouldBeFalse();
        send.ProviderMessageId.ShouldBe(1);
        send.CostRial.ShouldBe(RecordingCodeSender.Cost);
    }

    [Fact]
    public async Task Handle_LandlineNumber_IsRefusedAndNothingIsSent()
    {
        var result = await HandleAsync("02112345678");

        result.Error.ShouldBe(MemberErrors.PhoneNotMobile);
        _sender.Requests.ShouldBeEmpty();
    }

    // ---- Asking again ----

    [Fact]
    public async Task Handle_AgainWithinTwoMinutes_SendsNothing()
    {
        await HandleAsync(Phone);
        _time.Advance(TimeSpan.FromSeconds(100));

        var response = (await HandleAsync(Phone)).Value;

        response.Outcome.ShouldBe(RequestCodeOutcome.AlreadySent);
        response.SendAgainAt.ShouldBe(Start.AddMinutes(2));
        _sender.Requests.ShouldHaveSingleItem();
        (await SendsAsync()).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Handle_AgainAfterTwoMinutes_SendsTheSameCodeWithTheMinutesLeft()
    {
        await HandleAsync(Phone);
        _time.Advance(TimeSpan.FromMinutes(5));

        var response = (await HandleAsync(Phone)).Value;

        response.Outcome.ShouldBe(RequestCodeOutcome.SentAgain);
        response.ExpiresAt.ShouldBe(Start.AddMinutes(60));
        response.SendAgainAt.ShouldBe(Start.AddMinutes(7));
        _sender.Requests.Count.ShouldBe(2);
        _sender.Requests[1].Code.ShouldBe(_sender.Requests[0].Code);
        _sender.Requests[1].MinutesLeft.ShouldBe(55);
        _account.AskedAbout.ShouldBe([1L]);

        var sends = await SendsAsync();
        sends.Count.ShouldBe(2);
        sends.Count(send => send.IsSendAgain).ShouldBe(1);
        (await CodeAsync()).LastProviderMessageId.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_AgainAfterKavenegarDeliveredIt_SendsNothing()
    {
        await HandleAsync(Phone);
        _account.Deliveries[1] = SmsDelivery.Delivered;
        _time.Advance(TimeSpan.FromMinutes(3));

        var response = (await HandleAsync(Phone)).Value;

        response.Outcome.ShouldBe(RequestCodeOutcome.AlreadyDelivered);
        response.SendAgainAt.ShouldBe(Start.AddMinutes(60));
        _sender.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Handle_AgainAfterKavenegarSaidNotDelivered_SendsItAgain()
    {
        await HandleAsync(Phone);
        _account.Deliveries[1] = SmsDelivery.NotDelivered;
        _time.Advance(TimeSpan.FromMinutes(3));

        (await HandleAsync(Phone)).Value.Outcome.ShouldBe(RequestCodeOutcome.SentAgain);
    }

    [Fact]
    public async Task Handle_AfterTheCodeExpired_SendsANewCode()
    {
        await HandleAsync(Phone);
        var firstSeed = (await CodeAsync()).CodeSeed;
        _account.Deliveries[1] = SmsDelivery.Delivered;
        _time.Advance(TimeSpan.FromMinutes(60));

        var response = (await HandleAsync(Phone)).Value;

        response.Outcome.ShouldBe(RequestCodeOutcome.Sent);
        response.ExpiresAt.ShouldBe(Start.AddMinutes(120));
        var row = await CodeAsync();
        row.CodeSeed.ShouldNotBe(firstSeed);
        _sender.Requests[1].Code.ShouldBe(CodeFor(row));
        _sender.Requests[1].MinutesLeft.ShouldBe(60);
    }

    // ---- Limits ----

    [Fact]
    public async Task Handle_SixthSmsWithinAnHour_IsRefusedBeforeKavenegar()
    {
        await HandleAsync(Phone);
        for (var sendAgain = 1; sendAgain <= 4; sendAgain++)
        {
            _time.Advance(SignInCode.SendAgainAfter);
            (await HandleAsync(Phone)).Value.Outcome.ShouldBe(RequestCodeOutcome.SentAgain);
        }

        _time.Advance(SignInCode.SendAgainAfter);
        var result = await HandleAsync(Phone);

        result.Error.ShouldBe(SignInCodeErrors.PhoneHourlyLimitReached);
        _sender.Requests.Count.ShouldBe(SignInCode.MaxSendsPerPhonePerHour);
    }

    [Fact]
    public async Task Handle_AnHourAfterTheFirstOfFive_CountsOnlyTheLastHour()
    {
        await HandleAsync(Phone);
        for (var sendAgain = 1; sendAgain <= 4; sendAgain++)
        {
            _time.Advance(SignInCode.SendAgainAfter);
            await HandleAsync(Phone);
        }

        // The first send, at the start, is now more than an hour old; the four sends again are not.
        _time.SetUtcNow(Start.AddMinutes(61));
        var response = (await HandleAsync(Phone)).Value;

        response.Outcome.ShouldBe(RequestCodeOutcome.Sent);
    }

    [Fact]
    public async Task Handle_ThreeHundredCodesSentToday_IsRefusedBeforeKavenegar()
    {
        await AddSendsAsync(SignInCode.MaxSendsPerGymDay, Start.AddHours(-1));

        var result = await HandleAsync(Phone);

        result.Error.ShouldBe(SignInCodeErrors.GymDailyLimitReached);
        _sender.Requests.ShouldBeEmpty();
        (await CodesAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_ThreeHundredCodesSentYesterday_StillSends()
    {
        // 20:00 UTC the evening before is 23:30 in Tehran: yesterday, as the gym counts days.
        await AddSendsAsync(SignInCode.MaxSendsPerGymDay, Start.AddHours(-12));

        (await HandleAsync(Phone)).Value.Outcome.ShouldBe(RequestCodeOutcome.Sent);
    }

    [Fact]
    public async Task Handle_AfterFifteenWrongTriesToday_SendsNoNewCodeUntilTomorrow()
    {
        for (var code = 1; code <= SignInCode.MaxWrongTriesPerDay / SignInCode.MaxWrongTries; code++)
        {
            (await HandleAsync(Phone)).Value.Outcome.ShouldBe(RequestCodeOutcome.Sent);
            await KillTheCodeAsync();
        }

        var refused = await HandleAsync(Phone);

        refused.Error.ShouldBe(SignInCodeErrors.PhoneClosedForToday);
        _sender.Requests.Count.ShouldBe(SignInCode.MaxWrongTriesPerDay / SignInCode.MaxWrongTries);

        // 21:00 UTC is 00:30 the next day in Tehran: the gym's next day.
        _time.SetUtcNow(new DateTimeOffset(2026, 10, 26, 21, 0, 0, TimeSpan.Zero));
        (await HandleAsync(Phone)).Value.Outcome.ShouldBe(RequestCodeOutcome.Sent);
    }

    // ---- What Kavenegar answers ----

    [Fact]
    public async Task Handle_KavenegarRefuses_SavesNothingAndTheNextPressTriesAgain()
    {
        var sender = new RecordingCodeSender(new SmsSendResult(SmsSendOutcome.PermanentFailure, ErrorCode: 424));

        var refused = await HandleAsync(Phone, sender);

        refused.Error.ShouldBe(SignInCodeErrors.NotSent);
        (await CodesAsync()).ShouldBeEmpty();
        (await SendsAsync()).ShouldBeEmpty();

        (await HandleAsync(Phone, sender)).Value.Outcome.ShouldBe(RequestCodeOutcome.Sent);
        sender.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_NoAnswerFromKavenegar_KeepsTheCodeAndCountsTheSend()
    {
        // The request left and nothing came back: the SMS may be on its way, and paid for.
        var sender = new RecordingCodeSender(new SmsSendResult(SmsSendOutcome.Unknown));

        var response = (await HandleAsync(Phone, sender)).Value;

        response.Outcome.ShouldBe(RequestCodeOutcome.Sent);
        (await CodeAsync()).LastProviderMessageId.ShouldBeNull();
        var send = (await SendsAsync()).ShouldHaveSingleItem();
        send.ProviderMessageId.ShouldBeNull();
        send.CostRial.ShouldBeNull();

        // No id to ask about: «ارسال دوباره» sends again without asking.
        _time.Advance(SignInCode.SendAgainAfter);
        (await HandleAsync(Phone, sender)).Value.Outcome.ShouldBe(RequestCodeOutcome.SentAgain);
        _account.AskedAbout.ShouldBeEmpty();
    }

    // ---- Two presses at once ----

    [Fact]
    public async Task Handle_TwoPressesAtTheSameMoment_SendOneSms()
    {
        var sender = new SlowCodeSender();

        var first = HandleAsync(Phone, sender);
        await sender.FirstRequestArrived.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var second = HandleAsync(Phone, sender);

        // The second waits on the phone's lock while the first is still at "Kavenegar".
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        second.IsCompleted.ShouldBeFalse();
        sender.Release.SetResult();

        var outcomes = (await Task.WhenAll(first, second)).Select(result => result.Value.Outcome).ToList();

        outcomes.ShouldBe([RequestCodeOutcome.Sent, RequestCodeOutcome.AlreadySent]);
        sender.Requests.ShouldBe(1);
        (await SendsAsync()).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Handle_TwoPhonesAtTheDaysLastSms_SendOnlyOne()
    {
        await AddSendsAsync(SignInCode.MaxSendsPerGymDay - 1, Start.AddHours(-1));
        var sender = new SlowCodeSender();

        var first = HandleAsync("+989121234567", sender);
        await sender.FirstRequestArrived.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var second = HandleAsync("+989351234567", sender);

        // The second phone waits on the gym's lock while the first is still at "Kavenegar".
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        second.IsCompleted.ShouldBeFalse();
        sender.Release.SetResult();

        (await first).Value.Outcome.ShouldBe(RequestCodeOutcome.Sent);
        (await second).Error.ShouldBe(SignInCodeErrors.GymDailyLimitReached);
        sender.Requests.ShouldBe(1);
        (await SendsAsync()).Count.ShouldBe(SignInCode.MaxSendsPerGymDay);
    }

    // ---- A browser that goes away ----

    [Fact]
    public async Task Handle_RequestCancelledDuringTheSend_StillSavesAndCountsTheSend()
    {
        // The SMS may already be paid for: rolling it back would hide it from every limit.
        using var request = new CancellationTokenSource();
        var sender = new CancellingCodeSender(request);

        var result = await HandleUntilCancelledAsync(Phone, sender, request.Token);

        result.Value.Outcome.ShouldBe(RequestCodeOutcome.Sent);
        (await SendsAsync()).ShouldHaveSingleItem();
        (await CodeAsync()).LastProviderMessageId.ShouldBe(CancellingCodeSender.MessageId);
    }

    // ---- The audit log ----

    [Fact]
    public async Task Handle_ANewCodeInTheSameRow_NeverWritesEitherSeedToTheAuditLog()
    {
        await HandleAsync(Phone);
        var firstSeed = (await CodeAsync()).CodeSeed;
        _time.Advance(SignInCode.Lifetime);
        await HandleAsync(Phone);
        var secondSeed = (await CodeAsync()).CodeSeed;

        await using var scope = Fixture.CreateScope();
        var audit = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs
            .AsNoTracking()
            .Where(log => log.EntityType == nameof(SignInCode))
            .ToListAsync(TestContext.Current.CancellationToken);

        audit.Count.ShouldBe(2);
        var written = audit.Select(log => $"{log.OldValues} {log.NewValues}").ToList();
        written.ShouldAllBe(values => !values.Contains(firstSeed) && !values.Contains(secondSeed) && !values.Contains(nameof(SignInCode.CodeSeed)));
    }

    // ---- Helpers ----

    private Task<Result<RequestCodeResponse>> HandleAsync(string phone) => HandleAsync(phone, _sender);

    private Task<Result<RequestCodeResponse>> HandleAsync(string phone, IVerificationCodeSender sender) =>
        HandleUntilCancelledAsync(phone, sender, TestContext.Current.CancellationToken);

    private async Task<Result<RequestCodeResponse>> HandleUntilCancelledAsync(string phone, IVerificationCodeSender sender, CancellationToken cancellationToken)
    {
        await using var scope = Fixture.CreateScope();
        var services = scope.ServiceProvider;
        var calendar = new GymCalendar(_time, Options.Create(new GymCalendarOptions { TimeZone = "Asia/Tehran" }));

        var handler = new RequestCodeHandler(
            services.GetRequiredService<IAppDbContext>(),
            services.GetRequiredService<IPhoneNormalizer>(),
            services.GetRequiredService<ISignInCodes>(),
            sender,
            _account,
            calendar,
            _time,
            NullLogger<RequestCodeHandler>.Instance);

        return await handler.Handle(new RequestCodeCommand(phone), cancellationToken);
    }

    /// <summary>Five wrong tries through the real check, on this test's clock.</summary>
    private async Task KillTheCodeAsync()
    {
        var right = CodeFor(await CodeAsync());
        var wrong = right == "00000" ? "11111" : "00000";
        for (var attempt = 1; attempt <= SignInCode.MaxWrongTries; attempt++)
        {
            await using var scope = Fixture.CreateScope();
            var services = scope.ServiceProvider;
            var handler = new VerifyCodeHandler(
                services.GetRequiredService<IAppDbContext>(),
                services.GetRequiredService<IPhoneNormalizer>(),
                services.GetRequiredService<ISignInCodes>(),
                new GymCalendar(_time, Options.Create(new GymCalendarOptions { TimeZone = "Asia/Tehran" })),
                _time);

            await handler.Handle(new VerifyCodeCommand(Phone, wrong), TestContext.Current.CancellationToken);
        }
    }

    private string CodeFor(SignInCode row) =>
        Fixture.Services.GetRequiredService<ISignInCodes>().CodeFor(row.PhoneNumber, row.CodeSeed);

    private async Task<SignInCode> CodeAsync() => (await CodesAsync()).ShouldHaveSingleItem();

    private async Task<List<SignInCode>> CodesAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().SignInCodes
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<SignInCodeSend>> SendsAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().SignInCodeSends
            .AsNoTracking()
            .OrderBy(send => send.SentAt)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Codes sent to other phones, straight into the table.</summary>
    private async Task AddSendsAsync(int count, DateTimeOffset sentAt)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        for (var i = 0; i < count; i++)
        {
            db.SignInCodeSends.Add(SignInCodeSend.Record($"+98935{i:D7}", sentAt, isSendAgain: false, i + 1000, 1_150m));
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Kavenegar accepts the code, and the browser goes away at that moment.</summary>
    private sealed class CancellingCodeSender(CancellationTokenSource request) : IVerificationCodeSender
    {
        public const long MessageId = 777;

        public async Task<SmsSendResult> SendCodeAsync(VerificationCodeMessage message, CancellationToken cancellationToken)
        {
            await request.CancelAsync();

            return SmsSendResult.Sent(MessageId, RecordingCodeSender.Cost);
        }
    }

    /// <summary>Holds the first request at "Kavenegar" until the test releases it.</summary>
    private sealed class SlowCodeSender : IVerificationCodeSender
    {
        private int _requests;

        public TaskCompletionSource FirstRequestArrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Requests => _requests;

        public async Task<SmsSendResult> SendCodeAsync(VerificationCodeMessage message, CancellationToken cancellationToken)
        {
            var number = Interlocked.Increment(ref _requests);
            FirstRequestArrived.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);

            return SmsSendResult.Sent(number, RecordingCodeSender.Cost);
        }
    }
}
