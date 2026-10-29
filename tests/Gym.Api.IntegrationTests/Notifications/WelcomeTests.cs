using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Application.Notifications.SendWelcome;
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
/// The member's welcome when their first plan is sold (BUSINESS_RULES.md §10 <i>The welcome</i>, task
/// 10.10). The sales go through the API, so they ask the test host's recording scheduler for the
/// welcome; the job is then made as Hangfire would make it, on a clock pinned to the hour each test needs.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class WelcomeTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");

    private static readonly SmsRetrySchedule NoWait = new(3, [TimeSpan.Zero, TimeSpan.Zero]);

    private static int _phoneSuffix;

    // ---- Asked for by the sale ----

    [Fact]
    public async Task Assign_FirstPlan_AsksForTheWelcome()
    {
        await TurnOnAsync();
        var (client, token) = await OwnerClientAsync();
        var scheduler = Scheduler();

        var plan = await AssignOkAsync(client, token, (await AddMemberAsync("علی رضایی")).Id);

        scheduler.Asked.ShouldHaveSingleItem().SubscriptionId.ShouldBe(plan.Id);
    }

    [Fact]
    public async Task Assign_SecondPlan_AsksForNothing()
    {
        await TurnOnAsync();
        var (client, token) = await OwnerClientAsync();
        var member = await AddMemberAsync("علی رضایی");
        await AssignOkAsync(client, token, member.Id);
        var scheduler = Scheduler();

        await AssignOkAsync(client, token, member.Id);

        scheduler.Asked.ShouldBeEmpty();
    }

    [Fact]
    public async Task Assign_MemberWhoHadOnlySingleVisits_AsksForTheWelcome()
    {
        await TurnOnAsync();
        var (client, token) = await OwnerClientAsync();
        await TestPlans.SetPricesAsync(Fixture, singleVisitPrice: 150_000m);
        var member = await AddMemberAsync("مریم کاظمی");
        await SellSingleVisitOkAsync(client, token, member.Id);
        var scheduler = Scheduler();

        var plan = await AssignOkAsync(client, token, member.Id);

        scheduler.Asked.ShouldHaveSingleItem().SubscriptionId.ShouldBe(plan.Id);
    }

    [Fact]
    public async Task SellSingleVisit_AsksForNothing()
    {
        await TurnOnAsync();
        var (client, token) = await OwnerClientAsync();
        await TestPlans.SetPricesAsync(Fixture, singleVisitPrice: 150_000m);
        var scheduler = Scheduler();

        await SellSingleVisitOkAsync(client, token, (await AddMemberAsync("مریم کاظمی")).Id);

        scheduler.Asked.ShouldBeEmpty();
    }

    [Fact]
    public async Task Renew_AfterTheOnlyPlanWasCancelled_AsksForTheWelcome()
    {
        await TurnOnAsync(welcome: false);
        var (client, token) = await OwnerClientAsync();
        var member = await AddMemberAsync("علی رضایی");
        var cancelled = await AssignOkAsync(client, token, member.Id);
        await CancelOkAsync(client, token, cancelled.Id);
        await TurnOnAsync();
        var scheduler = Scheduler();

        using var response = await SendAsync(client, token, HttpMethod.Post, $"/api/members/{member.Id}/subscriptions/renew", body: null);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var renewed = (await response.Content.ReadFromJsonAsync<SubscriptionResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

        scheduler.Asked.ShouldHaveSingleItem().SubscriptionId.ShouldBe(renewed.Id);
    }

    [Fact]
    public async Task CheckIn_WithAFirstPlanSold_AsksForTheWelcomeAfterTheVisitIsSaved()
    {
        await TurnOnAsync();
        var (client, token) = await OwnerClientAsync();
        await TestPlans.AddAsync(Fixture);
        var member = await AddMemberAsync("سارا محمدی");
        var scheduler = Scheduler();

        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/members/{member.Id}/attendance/check-in",
            new { lockerId = TestLockers.IdOf(3), sale = new { kind = "Membership", sessionCount = 10 } });
        response.IsSuccessStatusCode.ShouldBeTrue();

        var plan = (await PlansAsync(member.Id)).ShouldHaveSingleItem();
        scheduler.Asked.ShouldHaveSingleItem().SubscriptionId.ShouldBe(plan.Id);
    }

    [Fact]
    public async Task CheckIn_WithASingleVisitSold_AsksForNothing()
    {
        await TurnOnAsync();
        var (client, token) = await OwnerClientAsync();
        await TestPlans.SetPricesAsync(Fixture, singleVisitPrice: 150_000m);
        var member = await AddMemberAsync("سارا محمدی");
        var scheduler = Scheduler();

        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/members/{member.Id}/attendance/check-in",
            new { lockerId = TestLockers.IdOf(3), sale = new { kind = "SingleVisit" } });
        response.IsSuccessStatusCode.ShouldBeTrue();

        scheduler.Asked.ShouldBeEmpty();
    }

    [Fact]
    public async Task CheckIn_SaleRolledBack_AsksForNothing()
    {
        // The locker is already taken: the check-in and the plan sold with it are both rolled back.
        await TurnOnAsync();
        var (client, token) = await OwnerClientAsync();
        await TestPlans.AddAsync(Fixture);
        var other = await AddMemberAsync("رضا کریمی");
        await AssignOkAsync(client, token, other.Id);
        (await TestLockers.CheckInAsync(client, token, other.Id, lockerNumber: 3)).Dispose();
        var member = await AddMemberAsync("سارا محمدی");
        var scheduler = Scheduler();

        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/members/{member.Id}/attendance/check-in",
            new { lockerId = TestLockers.IdOf(3), sale = new { kind = "Membership", sessionCount = 10 } });
        response.IsSuccessStatusCode.ShouldBeFalse();

        (await PlansAsync(member.Id)).ShouldBeEmpty();
        scheduler.Asked.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Assign_WelcomeOrAllSmsOff_AsksForNothing(bool allSms, bool welcome)
    {
        await TurnOnAsync(allSms, welcome);
        var (client, token) = await OwnerClientAsync();
        var scheduler = Scheduler();

        await AssignOkAsync(client, token, (await AddMemberAsync("علی رضایی")).Id);

        scheduler.Asked.ShouldBeEmpty();
    }

    [Fact]
    public async Task Queue_After2200_IsPutOffToTenTheNextMorningInTehran()
    {
        var day = new DateOnly(2026, 10, 19);
        var scheduler = Scheduler();
        var subscriptionId = Guid.CreateVersion7();

        await using var scope = Fixture.CreateScope();
        Welcomes(scope, At(day, 23, 10)).Queue(subscriptionId);

        scheduler.Asked.ShouldHaveSingleItem().ShouldBe((subscriptionId, (DateTimeOffset?)At(day.AddDays(1), 10, 0)));
    }

    [Fact]
    public async Task Queue_WithinTheHours_IsSentAtOnce()
    {
        var scheduler = Scheduler();
        var subscriptionId = Guid.CreateVersion7();

        await using var scope = Fixture.CreateScope();
        Welcomes(scope, At(new DateOnly(2026, 10, 19), 8, 0)).Queue(subscriptionId);

        scheduler.Asked.ShouldHaveSingleItem().At.ShouldBeNull();
    }

    // ---- The job ----

    [Fact]
    public async Task Handle_FirstPlan_SendsTheWelcomeToTheMember()
    {
        await TurnOnAsync();
        var (member, plan) = await FirstPlanAsync();
        var sender = new ScriptedSmsSender();

        var outcome = await HandleAsync(plan.Id, sender, At(Today(), 12, 0));

        outcome.ShouldBe(WelcomeOutcome.Sent);
        var welcome = (await NotificationsAsync()).ShouldHaveSingleItem();
        welcome.Kind.ShouldBe(NotificationKind.Welcome);
        welcome.MemberId.ShouldBe(member.Id);
        welcome.SubscriptionId.ShouldBe(plan.Id);
        welcome.Recipient.ShouldBe(member.PhoneNumber);
        welcome.Status.ShouldBe(NotificationStatus.Sent);
        welcome.Text.ShouldBe(SmsText.ForWelcome(member.FullName));
        sender.Requests.ShouldHaveSingleItem().Receptor.ShouldBe(member.PhoneNumber);
    }

    [Fact]
    public async Task Handle_UnpaidPlan_StillSendsTheWelcome()
    {
        // About joining, not about money (§10): nothing was paid on the plan.
        await TurnOnAsync();
        var (_, plan) = await FirstPlanAsync();

        var outcome = await HandleAsync(plan.Id, new ScriptedSmsSender(), At(Today(), 12, 0));

        outcome.ShouldBe(WelcomeOutcome.Sent);
    }

    [Fact]
    public async Task Handle_WelcomeTurnedOffSinceTheSale_SendsNothing()
    {
        await TurnOnAsync();
        var (_, plan) = await FirstPlanAsync();
        await TurnOnAsync(welcome: false);
        var sender = new ScriptedSmsSender();

        var outcome = await HandleAsync(plan.Id, sender, At(Today(), 12, 0));

        outcome.ShouldBe(WelcomeOutcome.Off);
        sender.Requests.ShouldBeEmpty();
        (await NotificationsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_PlanCancelledSinceTheSale_SendsNothing()
    {
        await TurnOnAsync();
        var (client, token) = await OwnerClientAsync();
        var (_, plan) = await FirstPlanAsync();
        await CancelOkAsync(client, token, plan.Id);
        var sender = new ScriptedSmsSender();

        var outcome = await HandleAsync(plan.Id, sender, At(Today(), 12, 0));

        outcome.ShouldBe(WelcomeOutcome.NoLongerDue);
        sender.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_FirstCancelledBeforeItsWelcome_TheNextPlanIsWelcomed()
    {
        await TurnOnAsync();
        var (client, token) = await OwnerClientAsync();
        var (member, first) = await FirstPlanAsync();
        await CancelOkAsync(client, token, first.Id);
        var scheduler = Scheduler();

        var next = await AssignOkAsync(client, token, member.Id);

        scheduler.Asked.ShouldHaveSingleItem().SubscriptionId.ShouldBe(next.Id);
        (await HandleAsync(first.Id, new ScriptedSmsSender(), At(Today(), 12, 0))).ShouldBe(WelcomeOutcome.NoLongerDue);
        (await HandleAsync(next.Id, new ScriptedSmsSender(), At(Today(), 12, 1))).ShouldBe(WelcomeOutcome.Sent);
        (await NotificationsAsync()).ShouldHaveSingleItem().SubscriptionId.ShouldBe(next.Id);
    }

    [Fact]
    public async Task Handle_FirstCancelledAfterItsWelcome_TheNextPlanGetsNone()
    {
        await TurnOnAsync();
        var (client, token) = await OwnerClientAsync();
        var (member, first) = await FirstPlanAsync();
        await HandleAsync(first.Id, new ScriptedSmsSender(), At(Today(), 12, 0));
        await CancelOkAsync(client, token, first.Id);
        var next = await AssignOkAsync(client, token, member.Id);
        var sender = new ScriptedSmsSender();

        var outcome = await HandleAsync(next.Id, sender, At(Today(), 12, 5));

        outcome.ShouldBe(WelcomeOutcome.AlreadyWritten);
        sender.Requests.ShouldBeEmpty();
        (await NotificationsAsync()).ShouldHaveSingleItem().SubscriptionId.ShouldBe(first.Id);
    }

    [Fact]
    public async Task Handle_MemberDeactivatedSinceTheSale_SendsNothing()
    {
        await TurnOnAsync();
        var (member, plan) = await FirstPlanAsync();
        await ExecuteAsync($"UPDATE members SET is_active = false WHERE id = '{member.Id}'");
        var sender = new ScriptedSmsSender();

        var outcome = await HandleAsync(plan.Id, sender, At(Today(), 12, 0));

        outcome.ShouldBe(WelcomeOutcome.NoLongerDue);
        sender.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_MadeTwice_SendsOneWelcome()
    {
        await TurnOnAsync();
        var (_, plan) = await FirstPlanAsync();
        var sender = new ScriptedSmsSender();

        await HandleAsync(plan.Id, sender, At(Today(), 12, 0));
        var second = await HandleAsync(plan.Id, sender, At(Today(), 12, 5));

        second.ShouldBe(WelcomeOutcome.AlreadyWritten);
        sender.Requests.Count.ShouldBe(1);
        (await NotificationsAsync()).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Handle_RowLeftPendingByAStoppedJob_MarksItUnknownAndSendsNothing()
    {
        await TurnOnAsync();
        var (member, plan) = await FirstPlanAsync();
        await using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Notifications.Add(Notification.ForWelcome(member.Id, plan.Id, member.PhoneNumber, "متن"));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var sender = new ScriptedSmsSender();

        var outcome = await HandleAsync(plan.Id, sender, At(Today(), 12, 0));

        outcome.ShouldBe(WelcomeOutcome.Interrupted);
        sender.Requests.ShouldBeEmpty();
        (await NotificationsAsync()).ShouldHaveSingleItem().Status.ShouldBe(NotificationStatus.Unknown);
    }

    [Fact]
    public async Task Handle_TimeComesAt2230_IsPutOffToTenAndSendsNothing()
    {
        await TurnOnAsync();
        var (_, plan) = await FirstPlanAsync();
        var scheduler = Scheduler();
        var sender = new ScriptedSmsSender();

        var outcome = await HandleAsync(plan.Id, sender, At(Today(), 22, 30));

        outcome.ShouldBe(WelcomeOutcome.Deferred);
        sender.Requests.ShouldBeEmpty();
        (await NotificationsAsync()).ShouldBeEmpty();
        scheduler.Asked.ShouldHaveSingleItem().ShouldBe((plan.Id, (DateTimeOffset?)At(Today().AddDays(1), 10, 0)));
    }

    [Fact]
    public async Task Handle_FailureThatMayPass_IsTriedAgainAndSent()
    {
        await TurnOnAsync();
        var (_, plan) = await FirstPlanAsync();
        var sender = new ScriptedSmsSender(new SmsSendResult(SmsSendOutcome.RetryableFailure, ErrorCode: 409));

        var outcome = await HandleAsync(plan.Id, sender, At(Today(), 12, 0));

        outcome.ShouldBe(WelcomeOutcome.Sent);
        sender.Requests.Count.ShouldBe(2);
        (await NotificationsAsync()).ShouldHaveSingleItem().Attempts.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_CreditUsedUp_IsFailed()
    {
        await TurnOnAsync();
        var (_, plan) = await FirstPlanAsync();
        var sender = new ScriptedSmsSender(new SmsSendResult(SmsSendOutcome.CreditExhausted, ErrorCode: 418));

        var outcome = await HandleAsync(plan.Id, sender, At(Today(), 12, 0));

        outcome.ShouldBe(WelcomeOutcome.Failed);
        (await NotificationsAsync()).ShouldHaveSingleItem().Status.ShouldBe(NotificationStatus.Failed);
    }

    // ---- Helpers ----

    private async Task<WelcomeOutcome> HandleAsync(
        Guid subscriptionId, ScriptedSmsSender sender, DateTimeOffset at, SmsRetrySchedule? retries = null)
    {
        var (time, calendar) = Clock(at);

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var handler = new SendWelcomeHandler(
            db,
            calendar,
            time,
            sender,
            retries ?? NoWait,
            new Welcomes(db, calendar, Scheduler(clear: false), NullLogger<Welcomes>.Instance),
            NullLogger<SendWelcomeHandler>.Instance);

        return await handler.Handle(subscriptionId, TestContext.Current.CancellationToken);
    }

    private Welcomes Welcomes(AsyncServiceScope scope, DateTimeOffset at)
    {
        var (_, calendar) = Clock(at);

        return new Welcomes(
            scope.ServiceProvider.GetRequiredService<IAppDbContext>(),
            calendar,
            Scheduler(clear: false),
            NullLogger<Welcomes>.Instance);
    }

    private static (FakeTimeProvider Time, GymCalendar Calendar) Clock(DateTimeOffset at)
    {
        var time = new FakeTimeProvider(at);

        return (time, new GymCalendar(time, Options.Create(new GymCalendarOptions { TimeZone = "Asia/Tehran" })));
    }

    /// <summary>The test host's scheduler (GymApiFactory), emptied of what earlier tests asked for.</summary>
    private RecordingWelcomeScheduler Scheduler(bool clear = true)
    {
        var scheduler = Fixture.Services.GetRequiredService<IWelcomeScheduler>()
            .ShouldBeOfType<RecordingWelcomeScheduler>();
        if (clear)
        {
            scheduler.Clear();
        }

        return scheduler;
    }

    /// <summary>All SMS and the welcome on, as the Owner would on the page; every other kind off.</summary>
    private async Task TurnOnAsync(bool allSms = true, bool welcome = true)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.SmsSettings.SingleAsync(TestContext.Current.CancellationToken);
        settings.Update(
            allSms,
            SmsKindSettings.Off,
            LowSessionsSettings.Off,
            SmsKindSettings.Off,
            SmsKindSettings.Off,
            ownerPhone: null,
            dailyReportEnabled: false,
            dailyReportPhone: null,
            dailyReportSendTime: SmsSettings.DefaultDailyReportSendTime,
            subscriptionReceiptEnabled: false,
            welcomeEnabled: welcome,
            welcomeBackEnabled: false,
            debtReminder: SmsKindSettings.Off,
            debtReminderMinAmount: null,
            absence: SmsKindSettings.Off,
            broadcastEnabled: false, trainerRequestEnabled: false).IsSuccess.ShouldBeTrue();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A new member sold their first plan, 10 sessions for 900,000, nothing paid.</summary>
    private async Task<(Member Member, SubscriptionResponse Plan)> FirstPlanAsync()
    {
        var (client, token) = await OwnerClientAsync();
        var member = await AddMemberAsync("رضا کریمی");
        var plan = await AssignOkAsync(client, token, member.Id);

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
        var member = TestMembers.Seed(fullName, $"+98918{suffix:D7}");

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

    private static async Task SellSingleVisitOkAsync(HttpClient client, string token, Guid memberId)
    {
        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/members/{memberId}/subscriptions/single-visit", body: null);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static async Task CancelOkAsync(HttpClient client, string token, Guid subscriptionId)
    {
        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/subscriptions/{subscriptionId}/cancel", new { reason = "اشتباه در ثبت" });
        response.IsSuccessStatusCode.ShouldBeTrue();
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

    private async Task<List<Domain.Subscriptions.Subscription>> PlansAsync(Guid memberId)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Subscriptions
            .AsNoTracking()
            .Where(subscription => subscription.MemberId == memberId && !subscription.IsSingleSession)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A moment on <paramref name="day"/> at the given time on Tehran's clock.</summary>
    private static DateTimeOffset At(DateOnly day, int hour, int minute)
    {
        var local = day.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Unspecified);

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Tehran), TimeSpan.Zero);
    }
}
