using System.Net;

using Gym.Api.Common;
using Gym.Api.Configuration;
using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.SiteAuth.SignIn;
using Gym.Domain.Members;
using Gym.Domain.SiteAuth;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.SiteAuth;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Gym.Api.IntegrationTests.SiteAuth;

/// <summary>
/// Signing in and signing up on the site (BUSINESS_RULES.md §15 <i>Signing up and signing in</i>),
/// through the real pipeline with the fake sender, on the real clock.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SiteSignInTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string Phone = "+989121234567";

    private static readonly DateOnly BirthDate = new(1995, 3, 21);

    // ---- A member's phone ----

    [Fact]
    public async Task SignIn_AMembersPhone_SignsInAsThatMember()
    {
        var member = await AddMemberAsync("سارا محمدی", Phone);
        using var client = Fixture.CreateClient();

        using var response = await client.SignInWithCodeAsync(Fixture, Phone);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("outcome").GetString().ShouldBe(nameof(SignInOutcome.SignedIn));
        var me = body.GetProperty("access").GetProperty("me").GetProperty("member");
        me.GetProperty("memberId").GetGuid().ShouldBe(member.Id);
        me.GetProperty("fullName").GetString().ShouldBe("سارا محمدی");
        me.GetProperty("isActive").GetBoolean().ShouldBeTrue();

        var cookie = response.GetSiteRefreshCookie().ShouldNotBeNull();
        cookie.Path.ToString().ShouldBe(SiteRefreshTokenCookie.Path);
        cookie.HttpOnly.ShouldBeTrue();
        cookie.Secure.ShouldBeTrue();

        (await AccountsAsync()).ShouldHaveSingleItem().MemberId.ShouldBe(member.Id);
    }

    [Fact]
    public async Task SignIn_TheSameMemberTwice_KeepsOneAccountAndOneMember()
    {
        await AddMemberAsync("سارا محمدی", Phone);
        using var client = Fixture.CreateClient();

        using var first = await client.SignInWithCodeAsync(Fixture, Phone);
        using var second = await client.SignInWithCodeAsync(Fixture, Phone);

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await AccountsAsync()).ShouldHaveSingleItem();
        (await MembersAsync()).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SignIn_WrongCode_Returns422AndSignsNobodyIn()
    {
        await AddMemberAsync("سارا محمدی", Phone);
        using var client = Fixture.CreateClient();
        var code = await client.RequestCodeAsync(Fixture, Phone);

        using var response = await client.SignInAsync(Phone, code == "00000" ? "11111" : "00000");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe(SignInCodeErrors.Wrong.Code);
        response.GetSiteRefreshCookie().ShouldBeNull();
        (await AccountsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task SignIn_ADeactivatedMember_SignsInAndIsToldTheyAreInactive()
    {
        var member = await AddMemberAsync("سارا محمدی", Phone);
        await DeactivateAsync(member.Id);
        using var client = Fixture.CreateClient();

        using var response = await client.SignInWithCodeAsync(Fixture, Phone);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetProperty("access").GetProperty("me").GetProperty("member").GetProperty("isActive").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Me_AfterTheDeskDeactivatesTheMember_SaysInactiveAtOnce()
    {
        var member = await AddMemberAsync("سارا محمدی", Phone);
        using var client = Fixture.CreateClient();
        var accessToken = await SignInForAccessTokenAsync(client);

        await DeactivateAsync(member.Id);
        using var response = await client.GetMeAsync(accessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetProperty("member").GetProperty("isActive").GetBoolean().ShouldBeFalse();
    }

    // ---- A new phone ----

    [Fact]
    public async Task SignIn_APhoneTheGymDoesNotKnow_GivesASignUpTicketAndMakesNothing()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.SignInWithCodeAsync(Fixture, Phone);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("outcome").GetString().ShouldBe(nameof(SignInOutcome.NeedsProfile));
        body.GetProperty("signUpTicket").GetString().ShouldNotBeNullOrWhiteSpace();
        response.GetSiteRefreshCookie().ShouldBeNull();
        (await MembersAsync()).ShouldBeEmpty();
        (await AccountsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task SignUp_WithTheTicket_MakesAnActiveMemberWithNoPlanAndSignsIn()
    {
        using var client = Fixture.CreateClient();
        var ticket = await TicketAsync(client);

        using var response = await client.SignUpAsync(ticket, "علی رضایی", BirthDate);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.ReadSiteRefreshToken().ShouldNotBeNullOrWhiteSpace();
        var member = (await MembersAsync()).ShouldHaveSingleItem();
        member.FullName.ShouldBe("علی رضایی");
        member.PhoneNumber.ShouldBe(Phone);
        member.BirthDate.ShouldBe(BirthDate);
        member.IsActive.ShouldBeTrue();
        (await AccountsAsync()).ShouldHaveSingleItem().MemberId.ShouldBe(member.Id);
        (await response.ReadJsonAsync()).GetProperty("me").GetProperty("member").GetProperty("memberId").GetGuid().ShouldBe(member.Id);

        // §15: no plan, and no welcome SMS: that waits for the first plan, at the desk.
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Subscriptions.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await db.Notifications.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task SignUp_TheAuditRows_AreRecordedAsTheSystem()
    {
        using var client = Fixture.CreateClient();
        using var response = await client.SignUpAsync(await TicketAsync(client), "علی رضایی", BirthDate);
        response.EnsureSuccessStatusCode();

        await using var scope = Fixture.CreateScope();
        var audit = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs
            .AsNoTracking()
            .Where(log => log.EntityType == nameof(Member) || log.EntityType == nameof(SiteAccount))
            .ToListAsync(TestContext.Current.CancellationToken);

        audit.Count.ShouldBe(2);
        audit.ShouldAllBe(log => log.UserId == null);
    }

    [Fact]
    public async Task SignUp_SentWithAStaffToken_IsStillRecordedAsTheSystem()
    {
        // The site's paths are read with the site's scheme only: a staff token there is never read.
        await TestUsers.CreateAsync(Fixture, "owner", Gym.Infrastructure.Identity.Roles.Owner);
        using var client = Fixture.CreateClient();
        var staffToken = await client.LoginForAccessTokenAsync("owner", TestUsers.Password);
        var ticket = await TicketAsync(client);
        var request = new HttpRequestMessage(HttpMethod.Post, SiteClient.SignUpPath)
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { ticket, fullName = "علی رضایی", birthDate = BirthDate }),
        }.WithBearer(staffToken);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Members.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).CreatedBy.ShouldBeNull();
        var audit = await db.AuditLogs.AsNoTracking()
            .Where(log => log.EntityType == nameof(Member))
            .ToListAsync(TestContext.Current.CancellationToken);
        audit.ShouldHaveSingleItem().UserId.ShouldBeNull();
    }

    [Fact]
    public async Task SignUp_TheSameTicketTwiceAtOnce_MakesOneMemberAndOneAccount()
    {
        using var client = Fixture.CreateClient();
        var ticket = await TicketAsync(client);

        var responses = await Task.WhenAll(
            client.SignUpAsync(ticket, "علی رضایی", BirthDate),
            client.SignUpAsync(ticket, "علی رضایی", BirthDate));

        responses.ShouldAllBe(response => response.StatusCode == HttpStatusCode.OK);
        (await MembersAsync()).ShouldHaveSingleItem();
        (await AccountsAsync()).ShouldHaveSingleItem();
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task SignUpTicket_AsABearerToken_OpensNothing()
    {
        await TestUsers.CreateAsync(Fixture, "owner", Gym.Infrastructure.Identity.Roles.Owner);
        using var client = Fixture.CreateClient();
        var ticket = await TicketAsync(client);

        using var me = await client.GetMeAsync(ticket);
        using var staff = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/api/auth/me").WithBearer(ticket),
            TestContext.Current.CancellationToken);

        me.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        staff.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SignUp_ThePhoneWasRegisteredAtTheDeskMeanwhile_SignsInThatMemberUnchanged()
    {
        using var client = Fixture.CreateClient();
        var ticket = await TicketAsync(client);
        var member = await AddMemberAsync("سارا محمدی", Phone);

        using var response = await client.SignUpAsync(ticket, "نام دیگر", BirthDate);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await MembersAsync()).ShouldHaveSingleItem().FullName.ShouldBe("سارا محمدی");
        (await AccountsAsync()).ShouldHaveSingleItem().MemberId.ShouldBe(member.Id);
    }

    [Fact]
    public async Task SignUp_ATicketThatIsNotOurs_IsRefused()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.SignUpAsync("not-a-ticket", "علی رضایی", BirthDate);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe(SiteAuthErrors.SignUpExpired.Code);
        (await MembersAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task SignUp_ATicketOlderThanFifteenMinutes_IsRefused()
    {
        var options = Fixture.Services.GetRequiredService<IOptions<SiteAuthOptions>>();
        var sixteenMinutesAgo = new FakeTimeProvider(TimeProvider.System.GetUtcNow().AddMinutes(-16));
        var ticket = new JwtSiteTokens(options, sixteenMinutesAgo).IssueSignUpTicket(Phone).Value;
        using var client = Fixture.CreateClient();

        using var response = await client.SignUpAsync(ticket, "علی رضایی", BirthDate);

        (await response.ReadErrorCodeAsync()).ShouldBe(SiteAuthErrors.SignUpExpired.Code);
    }

    [Fact]
    public async Task SignUp_TheAccessTokenAsATicket_IsRefused()
    {
        // Another audience: a token from a sign-in never passes for proof of a new phone.
        await AddMemberAsync("سارا محمدی", "+989351234567");
        using var client = Fixture.CreateClient();
        using var signIn = await client.SignInWithCodeAsync(Fixture, "+989351234567");
        var accessToken = (await signIn.ReadJsonAsync()).GetProperty("access").GetProperty("accessToken").GetString()!;

        using var response = await client.SignUpAsync(accessToken, "علی رضایی", BirthDate);

        (await response.ReadErrorCodeAsync()).ShouldBe(SiteAuthErrors.SignUpExpired.Code);
    }

    [Fact]
    public async Task SignUp_NoBirthDate_Returns400()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.SignUpAsync(await TicketAsync(client), "علی رضایی", birthDate: null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await MembersAsync()).ShouldBeEmpty();
    }

    // ---- The session ----

    [Fact]
    public async Task Refresh_TheSignInsCookie_GivesANewAccessTokenAndCookie()
    {
        await AddMemberAsync("سارا محمدی", Phone);
        using var client = Fixture.CreateClient();
        using var signIn = await client.SignInWithCodeAsync(Fixture, Phone);
        var cookie = signIn.ReadSiteRefreshToken();

        using var refreshed = await client.RefreshAsync(cookie);

        refreshed.StatusCode.ShouldBe(HttpStatusCode.OK);
        refreshed.ReadSiteRefreshToken().ShouldNotBe(cookie);
        var accessToken = (await refreshed.ReadJsonAsync()).GetProperty("accessToken").GetString()!;
        (await client.GetMeAsync(accessToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_ARotatedCookieAgain_EndsTheWholeSession()
    {
        await AddMemberAsync("سارا محمدی", Phone);
        using var client = Fixture.CreateClient();
        using var signIn = await client.SignInWithCodeAsync(Fixture, Phone);
        var first = signIn.ReadSiteRefreshToken();
        using var refreshed = await client.RefreshAsync(first);
        var second = refreshed.ReadSiteRefreshToken();

        using var reused = await client.RefreshAsync(first);
        using var afterReuse = await client.RefreshAsync(second);

        reused.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await reused.ReadErrorCodeAsync()).ShouldBe(SiteAuthErrors.SessionInvalid.Code);
        afterReuse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_TheSameCookieTwiceAtOnce_EndsTheWholeSession()
    {
        await AddMemberAsync("سارا محمدی", Phone);
        using var client = Fixture.CreateClient();
        using var signIn = await client.SignInWithCodeAsync(Fixture, Phone);
        var cookie = signIn.ReadSiteRefreshToken();

        var responses = await Task.WhenAll(client.RefreshAsync(cookie), client.RefreshAsync(cookie));

        // Either the second lost the race at the database, or came after and was a reuse: both
        // revoke the family, so whatever child came back no longer works either.
        responses.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBeLessThanOrEqualTo(1);
        foreach (var child in responses.Where(response => response.StatusCode == HttpStatusCode.OK).Select(response => response.ReadSiteRefreshToken()))
        {
            using var again = await client.RefreshAsync(child);
            again.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task Refresh_ACookieOlderThanSixtyDays_IsRefused()
    {
        var member = await AddMemberAsync("سارا محمدی", Phone);
        const string secret = "an-expired-site-refresh-token";
        await using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var sixtyOneDaysAgo = TimeProvider.System.GetUtcNow().AddDays(-61);
            var account = SiteAccount.ForMember(member.Id, sixtyOneDaysAgo);
            db.SiteAccounts.Add(account);
            db.SiteRefreshTokens.Add(SiteRefreshToken.Issue(account.Id, Gym.Application.Common.Security.RefreshTokenSecret.Hash(secret), sixtyOneDaysAgo));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var client = Fixture.CreateClient();
        using var response = await client.RefreshAsync(secret);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_AfterTheDeskChangedThePhone_IsRefused()
    {
        // The desk typed a stranger's number; the stranger signed in as this member; the desk corrects it.
        var member = await AddMemberAsync("سارا محمدی", Phone);
        using var client = Fixture.CreateClient();
        using var signIn = await client.SignInWithCodeAsync(Fixture, Phone);
        var cookie = signIn.ReadSiteRefreshToken();

        var corrected = await UpdatePhoneAsync(member.Id, "+989351234567");
        using var refreshed = await client.RefreshAsync(cookie);

        corrected.IsSuccess.ShouldBeTrue();
        refreshed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await using var scope = Fixture.CreateScope();
        var token = await scope.ServiceProvider.GetRequiredService<AppDbContext>().SiteRefreshTokens
            .AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
        token.RevokedReason.ShouldBe(Gym.Domain.Auth.RefreshTokenRevocationReason.PhoneChanged);
    }

    [Fact]
    public async Task Refresh_AfterTheDeskChangedOnlyTheName_StillWorks()
    {
        var member = await AddMemberAsync("سارا محمدی", Phone);
        using var client = Fixture.CreateClient();
        using var signIn = await client.SignInWithCodeAsync(Fixture, Phone);
        var cookie = signIn.ReadSiteRefreshToken();

        (await UpdatePhoneAsync(member.Id, Phone, "سارا محمدی‌نژاد")).IsSuccess.ShouldBeTrue();
        using var refreshed = await client.RefreshAsync(cookie);

        refreshed.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_AfterLogout_IsRefused()
    {
        await AddMemberAsync("سارا محمدی", Phone);
        using var client = Fixture.CreateClient();
        using var signIn = await client.SignInWithCodeAsync(Fixture, Phone);
        var cookie = signIn.ReadSiteRefreshToken();

        using var logout = await client.LogoutAsync(cookie);
        using var refreshed = await client.RefreshAsync(cookie);

        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        refreshed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_TheStaffsCookie_IsNotTheSites()
    {
        // The staff's cookie has another name and path: the site's refresh never sees it.
        using var client = Fixture.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, SiteClient.RefreshPath);
        request.Headers.Add("Cookie", $"{RefreshTokenCookie.Name}=anything");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Rate limit ----

    [Fact]
    public async Task SignIn_MoreRequestsThanTheLimitFromOneAddress_Returns429()
    {
        using var client = Fixture.CreateClient(new Dictionary<string, string?>
        {
            [$"{SiteSignInRateLimitOptions.SectionName}:PermitLimit"] = "2",
        });

        for (var request = 1; request <= 2; request++)
        {
            using var allowed = await client.SignInAsync(Phone, "12345");
            allowed.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity, $"request {request} is within the limit");
        }

        using var refused = await client.SignInAsync(Phone, "12345");

        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    // ---- Helpers ----

    private async Task<string> TicketAsync(HttpClient client)
    {
        using var signIn = await client.SignInWithCodeAsync(Fixture, Phone);

        return (await signIn.ReadJsonAsync()).GetProperty("signUpTicket").GetString()!;
    }

    private async Task<string> SignInForAccessTokenAsync(HttpClient client)
    {
        using var signIn = await client.SignInWithCodeAsync(Fixture, Phone);

        return (await signIn.ReadJsonAsync()).GetProperty("access").GetProperty("accessToken").GetString()!;
    }

    private async Task<Member> AddMemberAsync(string fullName, string phone)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var member = TestMembers.Seed(fullName, phone);
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    /// <summary>The desk's own edit of the member (task 2.1's handler), with a new phone or name.</summary>
    private async Task<Gym.Domain.Common.Result<Gym.Application.Members.MemberResponse>> UpdatePhoneAsync(
        Guid memberId, string phone, string fullName = "سارا محمدی")
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var member = await db.Members.AsNoTracking().SingleAsync(row => row.Id == memberId, TestContext.Current.CancellationToken);
        var handler = scope.ServiceProvider.GetRequiredService<Gym.Application.Members.UpdateMember.UpdateMemberHandler>();

        return await handler.Handle(
            memberId,
            new Gym.Application.Members.UpdateMember.UpdateMemberCommand(fullName, phone, member.Notes, member.BirthDate, member.Version),
            TestContext.Current.CancellationToken);
    }

    private async Task DeactivateAsync(Guid memberId)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var member = await db.Members.SingleAsync(row => row.Id == memberId, TestContext.Current.CancellationToken);
        member.Deactivate();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<Member>> MembersAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Members
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<SiteAccount>> AccountsAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().SiteAccounts
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}
