using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Application.Notifications.SendSubscriptionReceipt;
using Gym.Application.Subscriptions;
using Gym.Domain.Members;
using Gym.Domain.Notifications;
using Gym.Infrastructure.Calendar;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Gym.Api.IntegrationTests.Notifications;

/// <summary>
/// The member's receipt when a plan is fully paid (BUSINESS_RULES.md §10 <i>The subscription
/// receipt</i>, task 10.9). The payments go through the API, so they ask the test host's recording
/// scheduler for the receipt; the job is then made as Hangfire would make it, on a clock pinned to
/// the hour each test needs.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SubscriptionReceiptTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");

    private static readonly SmsRetrySchedule NoWait = new(3, [TimeSpan.Zero, TimeSpan.Zero]);

    private static int _phoneSuffix;

    // ---- Asked for by the payment ----

    [Fact]
    public async Task Pay_InTwoParts_AsksForOneReceiptWithTheLastPart()
    {
        await TurnOnAsync();
        var (client, token) = await OwnerClientAsync();
        var plan = await AssignOkAsync(client, token, (await AddMemberAsync("علی رضایی")).Id);
        var scheduler = Scheduler();

        await PayOkAsync(client, token, plan.Id, 600_000m);
        scheduler.Asked.ShouldBeEmpty();

        await PayOkAsync(client, token, plan.Id, 300_000m);
        scheduler.Asked.ShouldHaveSingleItem().SubscriptionId.ShouldBe(plan.Id);
    }

    [Fact]
    public async Task Pay_InFullAtOnce_AsksForTheReceipt()
    {
        await TurnOnAsync();
        var (client, token) = await OwnerClientAsync();
        var plan = await AssignOkAsync(client, token, (await AddMemberAsync("علی رضایی")).Id);
        var scheduler = Scheduler();

        await PayOkAsync(client, token, plan.Id, 900_000m);

        scheduler.Asked.ShouldHaveSingleItem().SubscriptionId.ShouldBe(plan.Id);
    }

    [Fact]
    public async Task Settle_ClearingAPlan_AsksForItsReceiptButNotForThePartPaidOne()
    {
        await TurnOnAsync();
        var (client, token) = await OwnerClientAsync();
        var member = await AddMemberAsync("علی رضایی");
        var first = await AssignOkAsync(client, token, member.Id);
        var second = await AssignOkAsync(client, token, member.Id);
        var scheduler = Scheduler();

        // Oldest first (§5): the first plan is cleared, the second gets the rest.
        using var response = await SendAsync(client, token, HttpMethod.Post, $"/api/members/{member.Id}/settlements", new
        {
            amount = 1_000_000m,
            method = "Cash",
            items = new[]
            {
                new { kind = "Subscription", id = first.Id, outstanding = 900_000m },
                new { kind = "Subscription", id = second.Id, outstanding = 900_000m },
            },
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        scheduler.Asked.ShouldHaveSingleItem().SubscriptionId.ShouldBe(first.Id);
    }

    [Fact]
    public async Task Pay_SingleVisit_AsksForNothing()
    {
        await TurnOnAsync();
        var (client, token) = await OwnerClientAsync();
        await TestPlans.SetPricesAsync(Fixture, singleVisitPrice: 150_000m);
        var visit = await SellSingleVisitOkAsync(client, token, (await AddMemberAsync("مریم کاظمی")).Id);
        var scheduler = Scheduler();

        await PayOkAsync(client, token, visit.Id, 150_000m);

        scheduler.Asked.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Pay_ReceiptOrAllSmsOff_AsksForNothing(bool allSms, bool receipt)
    {
        await TurnOnAsync(allSms, receipt);
        var (client, token) = await OwnerClientAsync();
        var plan = await AssignOkAsync(client, token, (await AddMemberAsync("علی رضایی")).Id);
        var scheduler = Scheduler();

        await PayOkAsync(client, token, plan.Id, 900_000m);

        scheduler.Asked.ShouldBeEmpty();
    }

    [Fact]
    public async Task Queue_After2200_IsPutOffToTenTheNextMorningInTehran()
    {
        var day = new DateOnly(2026, 10, 19);
        var scheduler = Scheduler();
        var subscriptionId = Guid.CreateVersion7();

        await using var scope = Fixture.CreateScope();
        Receipts(scope, At(day, 22, 30)).Queue(subscriptionId);

        scheduler.Asked.ShouldHaveSingleItem().ShouldBe((subscriptionId, (DateTimeOffset?)At(day.AddDays(1), 10, 0)));
    }

    [Fact]
    public async Task Queue_Before0800_IsPutOffToTenThatMorning()
    {
        var day = new DateOnly(2026, 10, 19);
        var scheduler = Scheduler();
        var subscriptionId = Guid.CreateVersion7();

        await using var scope = Fixture.CreateScope();
        Receipts(scope, At(day, 6, 0)).Queue(subscriptionId);

        scheduler.Asked.ShouldHaveSingleItem().At.ShouldBe(At(day, 10, 0));
    }

    [Fact]
    public async Task Queue_WithinTheHours_IsSentAtOnce()
    {
        var scheduler = Scheduler();
        var subscriptionId = Guid.CreateVersion7();

        await using var scope = Fixture.CreateScope();
        Receipts(scope, At(new DateOnly(2026, 10, 19), 21, 59)).Queue(subscriptionId);

        scheduler.Asked.ShouldHaveSingleItem().At.ShouldBeNull();
    }

    // ---- The job ----

    [Fact]
    public async Task Handle_FullyPaidPlan_SendsTheReceiptToTheMember()
    {
        await TurnOnAsync();
        var (member, plan) = await PaidPlanAsync();
        var sender = new ScriptedSmsSender();

        var outcome = await HandleAsync(plan.Id, sender, At(Today(), 12, 0));

        outcome.ShouldBe(ReceiptOutcome.Sent);
        var receipt = (await NotificationsAsync()).ShouldHaveSingleItem();
        receipt.Kind.ShouldBe(NotificationKind.SubscriptionReceipt);
        receipt.MemberId.ShouldBe(member.Id);
        receipt.SubscriptionId.ShouldBe(plan.Id);
        receipt.Recipient.ShouldBe(member.PhoneNumber);
        receipt.Status.ShouldBe(NotificationStatus.Sent);
        receipt.Text.ShouldBe(SmsText.ForSubscriptionReceipt(member.FullName, 10, plan.StartDate, plan.EndDate, 900_000m));
        sender.Requests.ShouldHaveSingleItem().Receptor.ShouldBe(member.PhoneNumber);
    }

    [Fact]
    public async Task Handle_PlanQueuedBehindAnother_GivesItsDaysInsteadOfDates()
    {
        await TurnOnAsync();
        var (client, token) = await OwnerClientAsync();
        var member = await AddMemberAsync("علی رضایی");
        await AssignOkAsync(client, token, member.Id);
        var queued = await AssignOkAsync(client, token, member.Id);
        await PayOkAsync(client, token, queued.Id, 900_000m);

        await HandleAsync(queued.Id, new ScriptedSmsSender(), At(Today(), 12, 0));

        (await NotificationsAsync()).ShouldHaveSingleItem().Text
            .ShouldBe(SmsText.ForQueuedSubscriptionReceipt(member.FullName, 10, 30, 900_000m));
    }

    [Fact]
    public async Task Handle_ReceiptTurnedOffSincePayment_SendsNothing()
    {
        await TurnOnAsync();
        var (_, plan) = await PaidPlanAsync();
        await TurnOnAsync(receipt: false);
        var sender = new ScriptedSmsSender();

        var outcome = await HandleAsync(plan.Id, sender, At(Today(), 12, 0));

        outcome.ShouldBe(ReceiptOutcome.Off);
        sender.Requests.ShouldBeEmpty();
        (await NotificationsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_PlanCancelledSincePayment_SendsNothing()
    {
        await TurnOnAsync();
        var (client, token) = await OwnerClientAsync();
        var (_, plan) = await PaidPlanAsync();
        using (var cancel = await SendAsync(client, token, HttpMethod.Post, $"/api/subscriptions/{plan.Id}/cancel", new { reason = "اشتباه در ثبت" }))
        {
            cancel.IsSuccessStatusCode.ShouldBeTrue();
        }

        var sender = new ScriptedSmsSender();

        var outcome = await HandleAsync(plan.Id, sender, At(Today(), 12, 0));

        outcome.ShouldBe(ReceiptOutcome.NoLongerDue);
        sender.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_MemberDeactivatedSincePayment_SendsNothing()
    {
        await TurnOnAsync();
        var (member, plan) = await PaidPlanAsync();
        await ExecuteAsync($"UPDATE members SET is_active = false WHERE id = '{member.Id}'");
        var sender = new ScriptedSmsSender();

        var outcome = await HandleAsync(plan.Id, sender, At(Today(), 12, 0));

        outcome.ShouldBe(ReceiptOutcome.NoLongerDue);
        sender.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_MadeTwice_SendsOneReceipt()
    {
        await TurnOnAsync();
        var (_, plan) = await PaidPlanAsync();
        var sender = new ScriptedSmsSender();

        await HandleAsync(plan.Id, sender, At(Today(), 12, 0));
        var second = await HandleAsync(plan.Id, sender, At(Today(), 12, 5));

        second.ShouldBe(ReceiptOutcome.AlreadyWritten);
        sender.Requests.Count.ShouldBe(1);
        (await NotificationsAsync()).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Handle_RowLeftPendingByAStoppedJob_MarksItUnknownAndSendsNothing()
    {
        await TurnOnAsync();
        var (member, plan) = await PaidPlanAsync();
        await using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Notifications.Add(Notification.ForSubscriptionReceipt(member.Id, plan.Id, member.PhoneNumber, "متن"));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var sender = new ScriptedSmsSender();

        var outcome = await HandleAsync(plan.Id, sender, At(Today(), 12, 0));

        outcome.ShouldBe(ReceiptOutcome.Interrupted);
        sender.Requests.ShouldBeEmpty();
        (await NotificationsAsync()).ShouldHaveSingleItem().Status.ShouldBe(NotificationStatus.Unknown);
    }

    [Fact]
    public async Task Handle_TimeComesAt2230_IsPutOffToTenAndSendsNothing()
    {
        await TurnOnAsync();
        var (_, plan) = await PaidPlanAsync();
        var scheduler = Scheduler();
        var sender = new ScriptedSmsSender();

        var outcome = await HandleAsync(plan.Id, sender, At(Today(), 22, 30));

        outcome.ShouldBe(ReceiptOutcome.Deferred);
        sender.Requests.ShouldBeEmpty();
        (await NotificationsAsync()).ShouldBeEmpty();
        scheduler.Asked.ShouldHaveSingleItem().ShouldBe((plan.Id, (DateTimeOffset?)At(Today().AddDays(1), 10, 0)));
    }

    [Fact]
    public async Task Handle_FailureThatMayPass_IsTriedAgainAndSent()
    {
        await TurnOnAsync();
        var (_, plan) = await PaidPlanAsync();
        var sender = new ScriptedSmsSender(new SmsSendResult(SmsSendOutcome.RetryableFailure, ErrorCode: 409));

        var outcome = await HandleAsync(plan.Id, sender, At(Today(), 12, 0));

        outcome.ShouldBe(ReceiptOutcome.Sent);
        sender.Requests.Count.ShouldBe(2);
        (await NotificationsAsync()).ShouldHaveSingleItem().Attempts.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_RetryWouldFallAfter2200_IsFailedAfterOneTry()
    {
        await TurnOnAsync();
        var (_, plan) = await PaidPlanAsync();
        var sender = new ScriptedSmsSender(new SmsSendResult(SmsSendOutcome.RetryableFailure, ErrorCode: 409));
        var oneMinute = new SmsRetrySchedule(3, [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)]);

        var outcome = await HandleAsync(plan.Id, sender, At(Today(), 21, 59, 30), oneMinute);

        outcome.ShouldBe(ReceiptOutcome.Failed);
        sender.Requests.Count.ShouldBe(1);
        var receipt = (await NotificationsAsync()).ShouldHaveSingleItem();
        receipt.Status.ShouldBe(NotificationStatus.Failed);
        receipt.ErrorCode.ShouldBe(409);
    }

    [Fact]
    public async Task Handle_CreditUsedUp_IsFailed()
    {
        await TurnOnAsync();
        var (_, plan) = await PaidPlanAsync();
        var sender = new ScriptedSmsSender(new SmsSendResult(SmsSendOutcome.CreditExhausted, ErrorCode: 418));

        var outcome = await HandleAsync(plan.Id, sender, At(Today(), 12, 0));

        outcome.ShouldBe(ReceiptOutcome.Failed);
        (await NotificationsAsync()).ShouldHaveSingleItem().Status.ShouldBe(NotificationStatus.Failed);
    }

    // ---- Helpers ----

    private async Task<ReceiptOutcome> HandleAsync(
        Guid subscriptionId, ScriptedSmsSender sender, DateTimeOffset at, SmsRetrySchedule? retries = null)
    {
        var (time, calendar) = Clock(at);

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var handler = new SendSubscriptionReceiptHandler(
            db,
            calendar,
            time,
            sender,
            retries ?? NoWait,
            new SubscriptionReceipts(db, calendar, Scheduler(clear: false), NullLogger<SubscriptionReceipts>.Instance),
            NullLogger<SendSubscriptionReceiptHandler>.Instance);

        return await handler.Handle(subscriptionId, TestContext.Current.CancellationToken);
    }

    private SubscriptionReceipts Receipts(AsyncServiceScope scope, DateTimeOffset at)
    {
        var (_, calendar) = Clock(at);

        return new SubscriptionReceipts(
            scope.ServiceProvider.GetRequiredService<IAppDbContext>(),
            calendar,
            Scheduler(clear: false),
            NullLogger<SubscriptionReceipts>.Instance);
    }

    private static (FakeTimeProvider Time, GymCalendar Calendar) Clock(DateTimeOffset at)
    {
        var time = new FakeTimeProvider(at);

        return (time, new GymCalendar(time, Options.Create(new GymCalendarOptions { TimeZone = "Asia/Tehran" })));
    }

    /// <summary>The test host's scheduler (GymApiFactory), emptied of what earlier tests asked for.</summary>
    private RecordingSubscriptionReceiptScheduler Scheduler(bool clear = true)
    {
        var scheduler = Fixture.Services.GetRequiredService<ISubscriptionReceiptScheduler>()
            .ShouldBeOfType<RecordingSubscriptionReceiptScheduler>();
        if (clear)
        {
            scheduler.Clear();
        }

        return scheduler;
    }

    /// <summary>All SMS and the receipt on, as the Owner would on the page; every other kind off.</summary>
    private async Task TurnOnAsync(bool allSms = true, bool receipt = true)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.SmsSettings.SingleAsync(TestContext.Current.CancellationToken);
        settings.Update(
            allSms,
            SmsKindSettings.Off,
            SmsKindSettings.Off,
            SmsKindSettings.Off,
            SmsKindSettings.Off,
            ownerPhone: null,
            dailyReportEnabled: false,
            dailyReportPhone: null,
            subscriptionReceiptEnabled: receipt,
            welcomeEnabled: false,
            debtReminder: SmsKindSettings.Off,
            debtReminderMinAmount: null).IsSuccess.ShouldBeTrue();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A member with a plan of 10 sessions for 900,000, paid in full today.</summary>
    private async Task<(Member Member, SubscriptionResponse Plan)> PaidPlanAsync()
    {
        var (client, token) = await OwnerClientAsync();
        var member = await AddMemberAsync("رضا کریمی");
        var plan = await AssignOkAsync(client, token, member.Id);
        await PayOkAsync(client, token, plan.Id, 900_000m);

        return (member, plan);
    }

    private async Task<(HttpClient Client, string Token)> OwnerClientAsync()
    {
        if (!await OwnerExistsAsync())
        {
            await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        }

        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("owner", TestUsers.Password));
    }

    private async Task<bool> OwnerExistsAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users
            .AnyAsync(user => user.UserName == "owner", TestContext.Current.CancellationToken);
    }

    private DateOnly Today() => Fixture.Services.GetRequiredService<IGymCalendar>().Today();

    private async Task<Member> AddMemberAsync(string fullName)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed(fullName, $"+98917{suffix:D7}");

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    private async Task<SubscriptionResponse> AssignOkAsync(HttpClient client, string token, Guid memberId)
    {
        var plan = await TestPlans.AddAsync(Fixture, price: 900_000m);

        using var response = await SendAsync(client, token, HttpMethod.Post, $"/api/members/{memberId}/subscriptions", plan.Body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<SubscriptionResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<SubscriptionResponse> SellSingleVisitOkAsync(HttpClient client, string token, Guid memberId)
    {
        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/members/{memberId}/subscriptions/single-visit", body: null);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<SubscriptionResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task PayOkAsync(HttpClient client, string token, Guid subscriptionId, decimal amount)
    {
        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/subscriptions/{subscriptionId}/payments", new { amount, method = "Card" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client, string token, HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database
            .ExecuteSqlRawAsync(sql, TestContext.Current.CancellationToken);
    }

    private async Task<List<Notification>> NotificationsAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A moment on <paramref name="day"/> at the given time on Tehran's clock.</summary>
    private static DateTimeOffset At(DateOnly day, int hour, int minute, int second = 0)
    {
        var local = day.ToDateTime(new TimeOnly(hour, minute, second), DateTimeKind.Unspecified);

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Tehran), TimeSpan.Zero);
    }
}
