using System.Globalization;
using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Application.Notifications;
using Gym.Application.Notifications.SendDailySms;
using Gym.Application.Reports;
using Gym.Application.Reports.GetAttendanceReport;
using Gym.Application.Reports.GetFinancialReport;
using Gym.Application.Subscriptions;
using Gym.Domain.Members;
using Gym.Domain.Notifications;
using Gym.Domain.Payments;
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
/// The Owner's daily report (BUSINESS_RULES.md §10 <i>The daily report</i>, task 10.7). The day's
/// visits and money are made through the API, so they land on the real today; the run is then built
/// as the job builds it, on a clock pinned to that day's evening in Tehran.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class DailyReportTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string ReportPhone = "+989351112233";

    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");

    private static readonly SmsRetrySchedule NoWait = new(3, [TimeSpan.Zero, TimeSpan.Zero]);

    private static int _phoneSuffix;

    // ---- The figures ----

    [Fact]
    public async Task FiguresAsync_BusyDay_AreTheDashboardsFiguresForTheDay()
    {
        var (client, token) = await OwnerClientAsync();

        // A plan paid partly by card and partly in cash, and the member comes in.
        var withPlan = await AddMemberAsync("علی رضایی");
        var plan = await AssignOkAsync(client, token, withPlan.Id);
        await PayOkAsync(client, token, $"/api/subscriptions/{plan.Id}/payments", 600_000m, "Card");
        await PayOkAsync(client, token, $"/api/subscriptions/{plan.Id}/payments", 300_000m, "Cash");
        await TestLockers.CheckInOkAsync(client, token, withPlan.Id);

        // A plan sold by mistake, paid by transfer, then cancelled: refunded by transfer, not sold.
        var mistake = await AssignOkAsync(client, token, (await AddMemberAsync("سارا امینی")).Id);
        await PayOkAsync(client, token, $"/api/subscriptions/{mistake.Id}/payments", 100_000m, "BankTransfer");
        await PostOkAsync(client, token, $"/api/subscriptions/{mistake.Id}/cancel", new { reason = "اشتباه در ثبت" });

        // A single visit paid in cash, used at once.
        await TestPlans.SetPricesAsync(Fixture, singleVisitPrice: 150_000m);
        var visitor = await AddMemberAsync("مریم کاظمی");
        var single = await SellSingleVisitOkAsync(client, token, visitor.Id);
        await PayOkAsync(client, token, $"/api/subscriptions/{single.Id}/payments", 150_000m, "Cash");
        await TestLockers.CheckInOkAsync(client, token, visitor.Id);

        // A guest is no attendance (§7).
        await TestGuests.CheckInOkAsync(client, token, lockerNumber: 10);

        var today = Today();
        var figures = await FiguresAsync(today);

        figures.ShouldBe(new DailyReportFigures(
            today,
            CheckIns: 2,
            PlansSold: 1,
            SingleVisitsSold: 1,
            Received: 1_050_000m,
            Cash: 450_000m,
            Card: 600_000m,
            BankTransfer: 0m));

        // The same day on the dashboard says the same.
        var query = $"from={Iso(today)}&to={Iso(today)}";
        var financial = (await GetOkAsync<FinancialReportResponse>(client, token, $"/api/reports/financial?{query}")).Current;
        var attendance = await GetOkAsync<AttendanceReportResponse>(client, token, $"/api/reports/attendance?{query}");
        figures.CheckIns.ShouldBe(attendance.Visits);
        figures.PlansSold.ShouldBe(financial.BySource.Single(row => row.Source == RevenueSource.Membership).Sold);
        figures.SingleVisitsSold.ShouldBe(financial.BySource.Single(row => row.Source == RevenueSource.SingleSession).Sold);
        figures.Received.ShouldBe(financial.ReceivedByMethod.Sum(row => row.Money.Net));
        figures.Cash.ShouldBe(financial.ReceivedByMethod.Single(row => row.Method == PaymentMethod.Cash).Money.Net);
    }

    // ---- The run ----

    [Fact]
    public async Task Handle_At2245_SendsTheDaysReportToItsOwnNumber()
    {
        await TurnOnAsync();
        await CheckInSomeoneAsync();
        var today = Today();
        var sender = new ScriptedSmsSender();

        var result = await RunAsync(sender, At(today, 22, 45));

        result.Sent.ShouldBe(1);
        var report = (await NotificationsAsync()).ShouldHaveSingleItem();
        report.Kind.ShouldBe(NotificationKind.DailyReport);
        report.ReportDate.ShouldBe(today);
        report.Recipient.ShouldBe(ReportPhone);
        report.Status.ShouldBe(NotificationStatus.Sent);
        report.Text.ShouldBe(SmsText.ForDailyReport(await FiguresAsync(today)));
        sender.Requests.ShouldHaveSingleItem().Receptor.ShouldBe(ReportPhone);
    }

    [Fact]
    public async Task Handle_DayWithNothing_SendsNothingAndWritesNoRow()
    {
        await TurnOnAsync();
        var sender = new ScriptedSmsSender();

        var result = await RunAsync(sender, At(Today(), 22, 45));

        result.Ran.ShouldBeTrue();
        sender.Requests.ShouldBeEmpty();
        (await NotificationsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_RunTwiceAndAMissedRunLate_SendsOneReport()
    {
        await TurnOnAsync();
        await CheckInSomeoneAsync();
        var sender = new ScriptedSmsSender();

        await RunAsync(sender, At(Today(), 22, 45));
        await RunAsync(sender, At(Today(), 23, 30));

        sender.Requests.Count.ShouldBe(1);
        (await NotificationsAsync()).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Handle_MissedRunMadeBeforeMidnight_IsSent()
    {
        await TurnOnAsync();
        await CheckInSomeoneAsync();
        var sender = new ScriptedSmsSender();

        var result = await RunAsync(sender, At(Today(), 23, 59, 30));

        result.Sent.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Before0800_SendsNothing()
    {
        await TurnOnAsync();
        await CheckInSomeoneAsync();
        var sender = new ScriptedSmsSender();

        var result = await RunAsync(sender, At(Today(), 7, 59));

        result.Ran.ShouldBeFalse();
        sender.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_RetryableAfter2200_IsTriedAgainBeforeMidnight()
    {
        await TurnOnAsync();
        await CheckInSomeoneAsync();
        var sender = new ScriptedSmsSender(new SmsSendResult(SmsSendOutcome.RetryableFailure, ErrorCode: 409));

        var result = await RunAsync(sender, At(Today(), 22, 45));

        result.Sent.ShouldBe(1);
        sender.Requests.Count.ShouldBe(2);
        (await NotificationsAsync()).ShouldHaveSingleItem().Attempts.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_ReportOff_SendsNothing()
    {
        await CheckInSomeoneAsync();
        var sender = new ScriptedSmsSender();

        var result = await RunAsync(sender, At(Today(), 22, 45));

        result.Ran.ShouldBeFalse();
        sender.Requests.ShouldBeEmpty();
    }

    // ---- Helpers ----

    private async Task<DailyReportFigures> FiguresAsync(DateOnly day)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<DailyReportCandidate>()
            .FiguresAsync(day, TestContext.Current.CancellationToken);
    }

    private async Task<SmsRunResult> RunAsync(ScriptedSmsSender sender, DateTimeOffset at)
    {
        var time = new FakeTimeProvider(at);
        var calendar = new GymCalendar(time, Options.Create(new GymCalendarOptions { TimeZone = "Asia/Tehran" }));

        await using var scope = Fixture.CreateScope();
        var handler = new SendDailySmsHandler(
            scope.ServiceProvider.GetRequiredService<IAppDbContext>(),
            calendar,
            time,
            sender,
            NoWait,
            scope.ServiceProvider.GetRequiredService<DailyReportCandidate>(),
            NullLogger<SendDailySmsHandler>.Instance);

        return await handler.Handle(NotificationKind.DailyReport, TestContext.Current.CancellationToken);
    }

    /// <summary>All SMS on and only the daily report, as the Owner would on the page.</summary>
    private async Task TurnOnAsync()
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.SmsSettings.SingleAsync(TestContext.Current.CancellationToken);
        settings.Update(
            enabled: true,
            SmsKindSettings.Off,
            SmsKindSettings.Off,
            SmsKindSettings.Off,
            SmsKindSettings.Off,
            ownerPhone: null,
            dailyReportEnabled: true,
            ReportPhone,
            subscriptionReceiptEnabled: false).IsSuccess.ShouldBeTrue();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>One member with a plan comes in today: a day that is not empty.</summary>
    private async Task CheckInSomeoneAsync()
    {
        var (client, token) = await OwnerClientAsync();
        var member = await AddMemberAsync("رضا کریمی");
        await AssignOkAsync(client, token, member.Id);
        await TestLockers.CheckInOkAsync(client, token, member.Id);
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

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task<Member> AddMemberAsync(string fullName)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed(fullName, $"+98916{suffix:D7}");

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

    private static async Task PayOkAsync(HttpClient client, string token, string path, decimal amount, string method)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, path, new { amount, method });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static async Task PostOkAsync(HttpClient client, string token, string path, object? body)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, path, body);
        response.IsSuccessStatusCode.ShouldBeTrue($"POST {path} answered {(int)response.StatusCode}.");
    }

    private static async Task<T> GetOkAsync<T>(HttpClient client, string token, string path)
        where T : class
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, path, body: null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
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
    private static DateTimeOffset At(DateOnly day, int hour, int minute, int second = 0)
    {
        var local = day.ToDateTime(new TimeOnly(hour, minute, second), DateTimeKind.Unspecified);

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Tehran), TimeSpan.Zero);
    }
}
