using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Application.Notifications.CheckSmsDelivery;
using Gym.Domain.Common;
using Gym.Domain.Notifications;
using Gym.Infrastructure.Jobs;
using Gym.Infrastructure.Persistence;

using Hangfire;
using Hangfire.Storage;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Gym.Api.IntegrationTests.Notifications;

/// <summary>
/// The nightly delivery check (BUSINESS_RULES.md §10 <i>Sending</i>, task 10.4): asks about every
/// message sent in the last 48 hours that is not yet delivered, blocked or cancelled, and keeps the
/// answer; a blocked or cancelled one costs nothing. The
/// provider is <see cref="ScriptedAccount"/>, which answers what each test scripts.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class CheckSmsDeliveryTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 20, 0, 0, TimeSpan.Zero);

    private static int _phoneSuffix;
    private static int _messageId;

    [Fact]
    public async Task Handle_SentWithin48Hours_KeepsWhatTheProviderSays()
    {
        var delivered = await AddSentAsync(Now.AddHours(-2));
        var notDelivered = await AddSentAsync(Now.AddHours(-47));
        var blocked = await AddSentAsync(Now.AddMinutes(-30));
        var account = new ScriptedAccount
        {
            [delivered] = SmsDelivery.Delivered,
            [notDelivered] = SmsDelivery.NotDelivered,
            [blocked] = SmsDelivery.BlockedByReceiver,
        };

        var changed = await CheckAsync(account);

        changed.ShouldBe(3);
        (await DeliveryOfAsync(delivered)).ShouldBe(SmsDelivery.Delivered);
        (await DeliveryOfAsync(notDelivered)).ShouldBe(SmsDelivery.NotDelivered);
        (await DeliveryOfAsync(blocked)).ShouldBe(SmsDelivery.BlockedByReceiver);
    }

    [Fact]
    public async Task Handle_SentMoreThan48HoursAgo_IsNotAskedAbout()
    {
        // The provider has forgotten it by now.
        var old = await AddSentAsync(Now.AddHours(-48));
        var account = new ScriptedAccount { [old] = SmsDelivery.Delivered };

        var changed = await CheckAsync(account);

        changed.ShouldBe(0);
        account.Asked.ShouldBeEmpty();
        (await DeliveryOfAsync(old)).ShouldBeNull();
    }

    [Fact]
    public async Task Handle_NotDeliveredYesterday_IsAskedAgainAndMayBecomeDelivered()
    {
        // The phone was off last night; it was turned on since.
        var message = await AddSentAsync(Now.AddHours(-25), SmsDelivery.NotDelivered);

        var changed = await CheckAsync(new ScriptedAccount { [message] = SmsDelivery.Delivered });

        changed.ShouldBe(1);
        (await DeliveryOfAsync(message)).ShouldBe(SmsDelivery.Delivered);
    }

    [Theory]
    [InlineData(SmsDelivery.BlockedByReceiver)]
    [InlineData(SmsDelivery.Cancelled)]
    public async Task Handle_BlockedOrCancelled_KeepsACostOfZero(SmsDelivery refunded)
    {
        // §10: Kavenegar gives the cost back for these, so the history's totals must not count it.
        var message = await AddSentAsync(Now.AddHours(-3));
        var cheaper = await AddSentAsync(Now.AddHours(-3));

        var changed = await CheckAsync(new ScriptedAccount { [message] = refunded, [cheaper] = SmsDelivery.Delivered });

        changed.ShouldBe(2);
        (await DeliveryOfAsync(message)).ShouldBe(refunded);
        (await CostOfAsync(message)).ShouldBe(0m);
        (await CostOfAsync(cheaper)).ShouldBe(1_350m);
    }

    [Theory]
    [InlineData(SmsDelivery.Delivered)]
    [InlineData(SmsDelivery.BlockedByReceiver)]
    [InlineData(SmsDelivery.Cancelled)]
    public async Task Handle_DeliveredBlockedOrCancelled_IsNotAskedAgain(SmsDelivery final)
    {
        await AddSentAsync(Now.AddHours(-3), final);
        var account = new ScriptedAccount();

        await CheckAsync(account);

        account.Asked.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_NotSent_IsNotAskedAbout()
    {
        await AddNotSentAsync(notification => notification.MarkFailed(424, Now.AddHours(-1)));
        await AddNotSentAsync(notification => notification.MarkUnknown(Now.AddHours(-1)));
        await AddNotSentAsync(_ => Result.Success());
        var account = new ScriptedAccount();

        var changed = await CheckAsync(account);

        changed.ShouldBe(0);
        account.Asked.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_ProviderStillOnItsWayOrNotAnswering_LeavesTheMessageAsItWas()
    {
        var onItsWay = await AddSentAsync(Now.AddHours(-1));
        var account = new ScriptedAccount();

        var changed = await CheckAsync(account);

        changed.ShouldBe(0);
        account.Asked.ShouldBe([onItsWay]);
        (await DeliveryOfAsync(onItsWay)).ShouldBeNull();
    }

    [Fact]
    public void Schedule_AtStartup_RunsEveryNightAt2330InTehran()
    {
        // §10: after the 22:00 limit on sending; every 24 hours, so inside the provider's 48.
        using var connection = Fixture.Services.GetRequiredService<JobStorage>().GetConnection();

        var job = connection.GetRecurringJobs().Single(job => job.Id == RecurringJobScheduler.SmsDeliveryCheckJobId);

        job.Cron.ShouldBe("30 23 * * *");
        job.TimeZoneId.ShouldBe(Fixture.Services.GetRequiredService<IGymCalendar>().TimeZone.Id);
        job.Job.Type.ShouldBe(typeof(CheckSmsDeliveryHandler));
        CheckSmsDeliveryHandler.ProviderMemory.ShouldBe(TimeSpan.FromHours(48));
    }

    // ---- Helpers ----

    private async Task<int> CheckAsync(ScriptedAccount account)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var handler = new CheckSmsDeliveryHandler(db, new FakeTimeProvider(Now), account, NullLogger<CheckSmsDeliveryHandler>.Instance);

        return await handler.Handle(TestContext.Current.CancellationToken);
    }

    /// <returns>The provider's id for the message.</returns>
    private async Task<long> AddSentAsync(DateTimeOffset sentAt, SmsDelivery? delivery = null)
    {
        var providerId = Interlocked.Increment(ref _messageId) + 1_000_000L;
        await AddNotSentAsync(notification =>
        {
            var sent = notification.MarkSent(providerId, 1_350m, sentAt);
            return delivery is { } known ? notification.RecordDelivery(known) : sent;
        });

        return providerId;
    }

    private async Task AddNotSentAsync(Func<Notification, Result> outcome)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed("سارا محمدی", $"+98913{suffix:D7}", new DateOnly(1990, 10, 9));
        var notification = Notification.ForBirthday(
            member.Id, 1405, member.PhoneNumber, SmsText.ForBirthday(member.FullName, new DateOnly(2026, 10, 9), new DateOnly(2026, 10, 9)));
        outcome(notification).IsSuccess.ShouldBeTrue();

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<SmsDelivery?> DeliveryOfAsync(long providerId)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Notifications
            .Where(notification => notification.ProviderMessageId == providerId)
            .Select(notification => notification.Delivery)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task<decimal?> CostOfAsync(long providerId)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Notifications
            .Where(notification => notification.ProviderMessageId == providerId)
            .Select(notification => notification.CostRial)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Answers with the deliveries it was given, and keeps the ids it was asked about.</summary>
    private sealed class ScriptedAccount : Dictionary<long, SmsDelivery>, ISmsAccount
    {
        public List<long> Asked { get; } = [];

        public Task<IReadOnlyDictionary<long, SmsDelivery>> GetDeliveriesAsync(
            IReadOnlyCollection<long> providerMessageIds, CancellationToken cancellationToken)
        {
            Asked.AddRange(providerMessageIds);
            IReadOnlyDictionary<long, SmsDelivery> answer = this
                .Where(pair => providerMessageIds.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            return Task.FromResult(answer);
        }

        public Task<SmsCredit> GetCreditAsync(CancellationToken cancellationToken) => Task.FromResult(SmsCredit.TestMode);
    }
}
