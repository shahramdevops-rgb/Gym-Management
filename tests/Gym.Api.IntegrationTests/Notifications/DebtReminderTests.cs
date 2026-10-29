using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Application.History.ListSales;
using Gym.Application.Notifications;
using Gym.Application.Notifications.SendDailySms;
using Gym.Application.ServiceCharges;
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
/// The member's debt reminder (BUSINESS_RULES.md §10 <i>The debt reminder</i>, task 10.11). The
/// sales are made through the API on the real today and then moved back in time, the way the
/// receivables' tests age a debt; the run is built as the job builds it, on a clock pinned to that
/// day's morning in Tehran.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class DebtReminderTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const int DaysAfter = 30;

    private const decimal MinAmount = 100_000m;

    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");

    private static readonly SmsRetrySchedule NoWait = new(3, [TimeSpan.Zero, TimeSpan.Zero]);

    private static int _phoneSuffix;

    // ---- Who gets it ----

    [Fact]
    public async Task Handle_OldDebt_SendsTheWholeOpenAccountOnceForTheOldestSale()
    {
        var (client, token) = await OwnerClientAsync();
        var member = await AddMemberAsync("سارا محمدی");
        var old = await AssignOkAsync(client, token, member.Id);
        await PayOkAsync(client, token, old.Id, 100_000m);
        await MoveSaleAsync(old.Id, daysAgo: 40);
        await AssignOkAsync(client, token, member.Id);
        await TurnOnAsync();
        var sender = new ScriptedSmsSender();

        await RunAsync(sender);
        await RunAsync(sender);

        // 800,000 still owed on the old plan and 900,000 on the new one: the whole open account.
        var request = sender.Requests.ShouldHaveSingleItem();
        request.Receptor.ShouldBe(member.PhoneNumber);
        request.Text.ShouldBe(SmsText.ForDebtReminder(member.FullName, 1_700_000m));
        var reminder = (await NotificationsAsync()).ShouldHaveSingleItem();
        reminder.Kind.ShouldBe(NotificationKind.DebtReminder);
        reminder.MemberId.ShouldBe(member.Id);
        reminder.DebtSince.ShouldBe(Today().AddDays(-40));
        reminder.Status.ShouldBe(NotificationStatus.Sent);
    }

    [Fact]
    public async Task Handle_OldestSaleExactlyTheDaysOld_IsSentADayYoungerIsNot()
    {
        var (client, token) = await OwnerClientAsync();
        var due = await AddMemberAsync("علی رضایی");
        await MoveSaleAsync((await AssignOkAsync(client, token, due.Id)).Id, daysAgo: DaysAfter);
        var notYet = await AddMemberAsync("مریم کاظمی");
        await MoveSaleAsync((await AssignOkAsync(client, token, notYet.Id)).Id, daysAgo: DaysAfter - 1);
        await TurnOnAsync();
        var sender = new ScriptedSmsSender();

        await RunAsync(sender);

        sender.Requests.ShouldHaveSingleItem().Receptor.ShouldBe(due.PhoneNumber);
    }

    [Fact]
    public async Task Handle_WholeDebtBelowTheMinimum_IsNotSent()
    {
        var (client, token) = await OwnerClientAsync();
        var member = await AddMemberAsync("رضا کریمی");
        var plan = await AssignOkAsync(client, token, member.Id);
        await PayOkAsync(client, token, plan.Id, 850_000m);
        await MoveSaleAsync(plan.Id, daysAgo: 40);
        await TurnOnAsync();
        var sender = new ScriptedSmsSender();

        await RunAsync(sender);

        sender.Requests.ShouldBeEmpty();
        (await NotificationsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_DeactivatedMember_GetsItToo()
    {
        var (client, token) = await OwnerClientAsync();
        var member = await AddMemberAsync("حسین امینی");
        await MoveSaleAsync((await AssignOkAsync(client, token, member.Id)).Id, daysAgo: 40);
        await DeactivateAsync(member.Id);
        await TurnOnAsync();
        var sender = new ScriptedSmsSender();

        await RunAsync(sender);

        sender.Requests.ShouldHaveSingleItem().Receptor.ShouldBe(member.PhoneNumber);
    }

    [Fact]
    public async Task Handle_GuestsOldDebt_IsNotSentToAnyone()
    {
        var (client, token) = await OwnerClientAsync();
        var guest = await TestGuests.CheckInOkAsync(client, token, lockerNumber: 3);
        var cardio = await RecordCardioOkAsync(client, token, guest.Id, 200_000m);
        await MoveChargeSaleAsync(cardio.Id, daysAgo: 40);
        await TurnOnAsync();
        var sender = new ScriptedSmsSender();

        await RunAsync(sender);

        sender.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_KindOff_SendsNothing()
    {
        var (client, token) = await OwnerClientAsync();
        var member = await AddMemberAsync("نرگس صالحی");
        await MoveSaleAsync((await AssignOkAsync(client, token, member.Id)).Id, daysAgo: 40);
        var sender = new ScriptedSmsSender();

        var result = await RunAsync(sender);

        result.Ran.ShouldBeFalse();
        sender.Requests.ShouldBeEmpty();
    }

    // ---- Once per debt ----

    [Fact]
    public async Task Handle_PartPaidAfterTheReminder_SendsNothingMore()
    {
        var (client, token) = await OwnerClientAsync();
        var member = await AddMemberAsync("سارا محمدی");
        var plan = await AssignOkAsync(client, token, member.Id);
        await MoveSaleAsync(plan.Id, daysAgo: 40);
        await TurnOnAsync();
        var sender = new ScriptedSmsSender();
        await RunAsync(sender);

        await PayOkAsync(client, token, plan.Id, 300_000m);
        await RunAsync(sender, Today().AddDays(1));

        sender.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_EverythingPaidThenANewDebt_GetsItsOwnReminder()
    {
        var (client, token) = await OwnerClientAsync();
        var member = await AddMemberAsync("علی رضایی");
        var old = await AssignOkAsync(client, token, member.Id);
        await MoveSaleAsync(old.Id, daysAgo: 70);
        await TurnOnAsync();
        var sender = new ScriptedSmsSender();
        await RunAsync(sender);

        // The reminder was written 35 days ago; everything owed then is paid; a plan sold after it is now 32 days old.
        await MoveRemindersAsync(member.Id, daysAgo: 35);
        await PayOkAsync(client, token, old.Id, 900_000m);
        var next = await AssignOkAsync(client, token, member.Id);
        await MoveSaleAsync(next.Id, daysAgo: 32);

        await RunAsync(sender);

        sender.Requests.Count.ShouldBe(2);
        sender.Requests[1].Text.ShouldBe(SmsText.ForDebtReminder(member.FullName, 900_000m));
        (await NotificationsAsync()).Select(notification => notification.DebtSince)
            .ShouldBe([Today().AddDays(-70), Today().AddDays(-32)], ignoreOrder: true);
    }

    [Fact]
    public async Task Handle_OldDebtPaidButANewerOneFromBeforeTheReminderLeft_SendsNothingMore()
    {
        var (client, token) = await OwnerClientAsync();
        var member = await AddMemberAsync("مریم کاظمی");
        var old = await AssignOkAsync(client, token, member.Id);
        await MoveSaleAsync(old.Id, daysAgo: 70);
        var newer = await AssignOkAsync(client, token, member.Id);
        await MoveSaleAsync(newer.Id, daysAgo: 50);
        await TurnOnAsync();
        var sender = new ScriptedSmsSender();
        await RunAsync(sender);

        // The newer plan was owed when the reminder was written, so it was already reminded of.
        await MoveRemindersAsync(member.Id, daysAgo: 35);
        await PayOkAsync(client, token, old.Id, 900_000m);

        await RunAsync(sender);

        sender.Requests.Count.ShouldBe(1);
    }

    // ---- Helpers ----

    /// <summary>All SMS on and only the debt reminder, at 11:00, as the Owner would on the page.</summary>
    private async Task TurnOnAsync()
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.SmsSettings.SingleAsync(TestContext.Current.CancellationToken);
        settings.Update(
            enabled: true,
            SmsKindSettings.Off,
            LowSessionsSettings.Off,
            SmsKindSettings.Off,
            SmsKindSettings.Off,
            ownerPhone: null,
            dailyReportEnabled: false,
            dailyReportPhone: null,
            dailyReportSendTime: SmsSettings.DefaultDailyReportSendTime,
            subscriptionReceiptEnabled: false,
            welcomeEnabled: false,
            welcomeBackEnabled: false,
            debtReminder: new SmsKindSettings(true, DaysAfter, new TimeOnly(11, 0)),
            debtReminderMinAmount: MinAmount,
            absence: SmsKindSettings.Off,
            broadcastEnabled: false, trainerRequestEnabled: false).IsSuccess.ShouldBeTrue();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>The run as the job makes it, at 11:00 in Tehran on <paramref name="day"/> (today by default).</summary>
    private async Task<SmsRunResult> RunAsync(ScriptedSmsSender sender, DateOnly? day = null)
    {
        var time = new FakeTimeProvider(At(day ?? Today(), 11, 0));
        var calendar = new GymCalendar(time, Options.Create(new GymCalendarOptions { TimeZone = "Asia/Tehran" }));

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var handler = new SendDailySmsHandler(
            db,
            calendar,
            new SmsBatchSender(db, calendar, time, sender, NoWait),
            scope.ServiceProvider.GetRequiredService<DailyReportCandidate>(),
            new DebtReminderCandidate(db, scope.ServiceProvider.GetRequiredService<SaleRows>(), calendar),
            new AbsenceCandidate(db, calendar),
            NullLogger<SendDailySmsHandler>.Instance);

        return await handler.Handle(NotificationKind.DebtReminder, TestContext.Current.CancellationToken);
    }

    private async Task<(HttpClient Client, string Token)> OwnerClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("owner", TestUsers.Password));
    }

    private IGymCalendar Calendar() => Fixture.Services.GetRequiredService<IGymCalendar>();

    private DateOnly Today() => Calendar().Today();

    private async Task<Member> AddMemberAsync(string fullName)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed(fullName, $"+98915{suffix:D7}");

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    private async Task DeactivateAsync(Guid memberId)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var member = await db.Members.SingleAsync(row => row.Id == memberId, TestContext.Current.CancellationToken);
        member.Deactivate();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>The default plan: 10 sessions, 30 days, 900,000.</summary>
    private async Task<SubscriptionResponse> AssignOkAsync(HttpClient client, string token, Guid memberId)
    {
        var plan = await TestPlans.AddAsync(Fixture);

        using var response = await SendAsync(client, token, HttpMethod.Post, $"/api/members/{memberId}/subscriptions", plan.Body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<SubscriptionResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task PayOkAsync(HttpClient client, string token, Guid subscriptionId, decimal amount)
    {
        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/subscriptions/{subscriptionId}/payments", new { amount, method = "Cash" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static async Task<ServiceChargeResponse> RecordCardioOkAsync(
        HttpClient client, string token, Guid attendanceId, decimal amount)
    {
        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/attendance/{attendanceId}/service-charges", new { kind = "Cardio", amount });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<ServiceChargeResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    /// <summary>The sale recorded at noon in Tehran, <paramref name="daysAgo"/> days before today.</summary>
    private async Task MoveSaleAsync(Guid subscriptionId, int daysAgo)
    {
        var soldAt = At(Today().AddDays(-daysAgo), 12, 0);

        await using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"UPDATE subscriptions SET created_at = {soldAt} WHERE id = {subscriptionId}",
            TestContext.Current.CancellationToken);
    }

    private async Task MoveChargeSaleAsync(Guid chargeId, int daysAgo)
    {
        var soldAt = At(Today().AddDays(-daysAgo), 12, 0);

        await using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"UPDATE service_charges SET created_at = {soldAt} WHERE id = {chargeId}",
            TestContext.Current.CancellationToken);
    }

    /// <summary>The member's reminders written at noon in Tehran, <paramref name="daysAgo"/> days before today.</summary>
    private async Task MoveRemindersAsync(Guid memberId, int daysAgo)
    {
        var writtenAt = At(Today().AddDays(-daysAgo), 12, 0);

        await using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"UPDATE notifications SET created_at = {writtenAt} WHERE member_id = {memberId} AND kind = 'DebtReminder'",
            TestContext.Current.CancellationToken);
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

    private async Task<List<Notification>> NotificationsAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A moment on <paramref name="day"/> at the given time on Tehran's clock.</summary>
    private static DateTimeOffset At(DateOnly day, int hour, int minute)
    {
        var local = day.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Unspecified);

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Tehran), TimeSpan.Zero);
    }
}
