using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common;
using Gym.Application.Members;
using Gym.Application.Notifications.GetSignInCodeSms;
using Gym.Application.Reports.GetNeedsAttention;
using Gym.Domain.SiteAuth;
using Gym.Infrastructure.Calendar;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Gym.Api.IntegrationTests.SiteAuth;

/// <summary>
/// What the panel shows of the site (BUSINESS_RULES.md §15, task 15.2): a member's site account on
/// their profile, the code SMS in «پیامک‌ها», and the day's 300 in «نیاز به اقدام».
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SiteInThePanelTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string Phone = "+989121234567";

    /// <summary>10:00 UTC is 13:30 in Tehran, on 5 Aban 1405; Aban began on 23 October.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 10, 0, 0, TimeSpan.Zero);

    // ---- The member's profile ----

    [Fact]
    public async Task GetMember_WhoSignedInOnTheSite_ShowsTheAccount()
    {
        var member = await AddMemberAsync();
        using var site = Fixture.CreateClient();
        (await site.SignInWithCodeAsync(Fixture, Phone)).EnsureSuccessStatusCode();
        var (client, owner, _) = await ClientsAsync();

        var profile = await GetMemberAsync(client, owner, member.Id);

        var account = profile.SiteAccount.ShouldNotBeNull();
        account.LastSignedInAt.ShouldBe(account.CreatedAt, TimeSpan.FromMinutes(1), "made by the first sign-in.");
    }

    [Fact]
    public async Task GetMember_WhoNeverSignedIn_HasNoAccount()
    {
        var member = await AddMemberAsync();
        var (client, _, staff) = await ClientsAsync();

        var profile = await GetMemberAsync(client, staff, member.Id);

        profile.SiteAccount.ShouldBeNull();
    }

    // ---- The code SMS in «پیامک‌ها» ----

    [Fact]
    public async Task SignInCodeSms_SendsTodayAndThisMonth_CountsEachAndTheCost()
    {
        await AddSendsAsync(
            Send(Now.AddHours(-1), costRial: 1_200m),
            Send(Now.AddMinutes(-5), costRial: null), // no answer: counted, no cost
            Send(new DateTimeOffset(2026, 10, 24, 8, 0, 0, TimeSpan.Zero), costRial: 1_200m), // 2 Aban
            Send(new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero), costRial: 1_200m)); // Mehr

        var response = await HandlePinnedAsync();

        response.Today.ShouldBe(2);
        response.ThisMonth.ShouldBe(3);
        response.ThisMonthCostRial.ShouldBe(2_400m);
        response.DailyLimit.ShouldBe(SignInCode.MaxSendsPerGymDay);
        response.LimitReached.ShouldBeFalse();
    }

    [Fact]
    public async Task SignInCodeSms_NoSendsYet_IsAllZero()
    {
        var response = await HandlePinnedAsync();

        response.Today.ShouldBe(0);
        response.ThisMonth.ShouldBe(0);
        response.ThisMonthCostRial.ShouldBe(0m);
    }

    [Fact]
    public async Task SignInCodeSms_AsStaff_Returns403()
    {
        var (client, _, staff) = await ClientsAsync();

        using var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/api/sms/sign-in-codes").WithBearer(staff),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- «نیاز به اقدام» ----

    [Fact]
    public async Task NeedsAttention_TodaysThreeHundredUsedUp_SaysSo()
    {
        var now = TimeProvider.System.GetUtcNow();
        await AddSendsAsync([.. Enumerable.Range(0, SignInCode.MaxSendsPerGymDay).Select(_ => Send(now, costRial: 1_200m))]);
        var (client, owner, _) = await ClientsAsync();

        var needs = await GetNeedsAttentionAsync(client, owner);

        needs.SignInCodeLimitReached.ShouldBeTrue();
    }

    [Fact]
    public async Task NeedsAttention_OneSendShortOfTheLimit_SaysNothing()
    {
        var now = TimeProvider.System.GetUtcNow();
        await AddSendsAsync([.. Enumerable.Range(0, SignInCode.MaxSendsPerGymDay - 1).Select(_ => Send(now, costRial: 1_200m))]);
        var (client, owner, _) = await ClientsAsync();

        var needs = await GetNeedsAttentionAsync(client, owner);

        needs.SignInCodeLimitReached.ShouldBeFalse();
    }

    // ---- Helpers ----

    private static SignInCodeSend Send(DateTimeOffset sentAt, decimal? costRial) =>
        SignInCodeSend.Record(Phone, sentAt, isSendAgain: false, providerMessageId: costRial is null ? null : 1, costRial);

    /// <summary>The handler under a clock pinned to <see cref="Now"/>.</summary>
    private async Task<SignInCodeSmsResponse> HandlePinnedAsync()
    {
        var time = new FakeTimeProvider(Now);
        var calendar = new GymCalendar(time, Options.Create(new GymCalendarOptions { TimeZone = "Asia/Tehran" }));
        await using var scope = Fixture.CreateScope();
        var handler = new GetSignInCodeSmsHandler(scope.ServiceProvider.GetRequiredService<IAppDbContext>(), calendar, time);

        return await handler.Handle(TestContext.Current.CancellationToken);
    }

    private async Task AddSendsAsync(params SignInCodeSend[] sends)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.SignInCodeSends.AddRange(sends);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Gym.Domain.Members.Member> AddMemberAsync()
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var member = TestMembers.Seed("سارا محمدی", Phone);
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    private async Task<(HttpClient Client, string Owner, string Staff)> ClientsAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client,
            await client.LoginForAccessTokenAsync("owner", TestUsers.Password),
            await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    private static async Task<MemberResponse> GetMemberAsync(HttpClient client, string token, Guid memberId)
    {
        using var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, $"/api/members/{memberId}").WithBearer(token),
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<MemberResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<NeedsAttentionResponse> GetNeedsAttentionAsync(HttpClient client, string token)
    {
        using var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/api/reports/needs-attention").WithBearer(token),
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<NeedsAttentionResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }
}
