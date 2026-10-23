using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Notifications.GetSmsSamples;
using Gym.Domain.Notifications;
using Gym.Domain.Payables;
using Gym.Infrastructure.Calendar;
using Gym.Infrastructure.Identity;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Gym.Api.IntegrationTests.Notifications;

/// <summary>
/// Each kind's sample text on «تنظیمات پیامک»: <c>GET /api/sms/samples</c> (BUSINESS_RULES.md §10
/// <i>SMS settings</i>, task 10.15). Owner only; built by <see cref="SmsText"/>, so it is the real wording.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SmsSamplesTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string Path = "/api/sms/samples";

    /// <summary>1405/07/28, 10:00 in Tehran.</summary>
    private static readonly DateOnly Today = new(2026, 10, 20);

    [Fact]
    public void Handle_EveryKind_IsTheTextTheRunsWriteWithItsParts()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 20, 6, 30, 0, TimeSpan.Zero));
        var calendar = new GymCalendar(time, Options.Create(new GymCalendarOptions { TimeZone = "Asia/Tehran" }));

        var samples = new GetSmsSamplesHandler(calendar).Handle();

        samples.SubscriptionExpiring.Text.ShouldBe(SmsText.ForRunningOut("علی", Today.AddDays(3), sessionsLeft: 4));
        samples.LowSessions.Text.ShouldBe("علی عزیز، فقط ۲ جلسه از اشتراک شما در باشگاه پاسارگاد مانده است.\nلغو11");
        samples.Birthday.Text.ShouldBe(SmsText.ForBirthday("علی", Today.AddDays(3)));
        samples.PayableDue.Text.ShouldBe(
            SmsText.ForPayableDue(PayableKind.Cheque, 12_500_000m, Today.AddDays(2), "فروشگاه تجهیزات ورزشی"));
        samples.DailyReport.Text.ShouldStartWith("گزارش باشگاه پاسارگاد ۰۷/۲۸\n");
        samples.SubscriptionReceipt.Text.ShouldBe(
            SmsText.ForSubscriptionReceipt("علی", 12, Today, Today.AddDays(30), 1_800_000m));
        samples.Welcome.Text.ShouldBe("درود بر علی عزیز، به باشگاه پاسارگاد خوش اومدی، امیدوارم ساعات خوشی رو توی باشگاه داشته باشی\nلغو11");
        samples.DebtReminder.Text.ShouldBe(SmsText.ForDebtReminder("علی", 450_000m));
        samples.Absence.Text.ShouldBe("علی عزیز، جات توی باشگاه پاسارگاد خالیه! ۱۲ روزه که باشگاه نیومدی و منتظر حضور گرمت هستیم!\nلغو11");
        samples.Absence.Parts.ShouldBe(2);

        foreach (var sample in new[]
                 {
                     samples.SubscriptionExpiring, samples.LowSessions, samples.Birthday, samples.PayableDue,
                     samples.DailyReport, samples.SubscriptionReceipt, samples.Welcome, samples.DebtReminder,
                     samples.Absence,
                 })
        {
            sample.Text.ShouldEndWith($"\n{SmsText.OptOut}");
            sample.Parts.ShouldBe(SmsParts.Count(sample.Text));
        }

        samples.Welcome.Parts.ShouldBe(2);
    }

    [Fact]
    public async Task Get_AsOwner_ReturnsEveryKind()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        var client = Fixture.CreateClient();
        var token = await client.LoginForAccessTokenAsync("owner", TestUsers.Password);
        using var request = new HttpRequestMessage(HttpMethod.Get, Path);

        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var samples = (await response.Content.ReadFromJsonAsync<SmsSamplesResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull();
        samples.Welcome.ShouldBe(new SmsSample("درود بر علی عزیز، به باشگاه پاسارگاد خوش اومدی، امیدوارم ساعات خوشی رو توی باشگاه داشته باشی\nلغو11", 2));
        samples.DailyReport.Text.ShouldContain("کارت:");
    }

    [Fact]
    public async Task Get_AsStaff_Returns403()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();
        var token = await client.LoginForAccessTokenAsync("staff", TestUsers.Password);
        using var request = new HttpRequestMessage(HttpMethod.Get, Path);

        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
