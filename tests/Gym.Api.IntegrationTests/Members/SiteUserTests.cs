using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Api.IntegrationTests.SiteAuth;
using Gym.Application.Common.Paging;
using Gym.Application.Members;
using Gym.Application.Notifications.Broadcasts;
using Gym.Application.SiteUsers;
using Gym.Application.SiteUsers.FindSiteUserByPhone;
using Gym.Domain.Cafe;
using Gym.Domain.Members;
using Gym.Domain.Notifications;
using Gym.Domain.SiteAuth;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Members;

/// <summary>
/// Site users (BUSINESS_RULES.md §16 <i>Site users</i>, task 16.1): someone who signed up on the site
/// and has had no plan and no visit is left out of the gym's lists, searches and groups, shown on
/// the «کاربران سایت» page, brought in from the new-member form, and becomes a member by a plan or
/// a visit, for good.
/// </summary>
/// <remarks>
/// Most tests seed a site user the way the sign-up leaves one (<see cref="Member.SignUpOnSite"/> and
/// its account); one goes through the real sign-up end to end.
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SiteUserTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string MembersPath = "/api/members";
    private const string SiteUsersPath = "/api/site-users";

    private static readonly DateOnly BirthDate = new(1995, 4, 20);

    // ---- Left out of the gym's work ----

    [Fact]
    public async Task SignUp_OnTheSite_IsMissingFromTheMemberListAndOnTheSiteUsersPage()
    {
        var (client, token) = await StaffClientAsync();
        using var site = Fixture.CreateClient();
        using var signIn = await site.SignInWithCodeAsync(Fixture, "+989121234567");
        var ticket = (await signIn.ReadJsonAsync()).GetProperty("signUpTicket").GetString()!;
        using var signUp = await site.SignUpAsync(ticket, "سارا محمدی", BirthDate);
        signUp.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await ListMembersAsync(client, token, "")).TotalCount.ShouldBe(0);
        var siteUser = (await ListSiteUsersAsync(client, token, "")).Items.ShouldHaveSingleItem();
        siteUser.FullName.ShouldBe("سارا محمدی");
        siteUser.PhoneNumber.ShouldBe("+989121234567");
        siteUser.BirthDate.ShouldBe(BirthDate);
    }

    [Theory]
    [InlineData("سارا")]
    [InlineData("4567")]
    [InlineData("09121234567")]
    public async Task ListMembers_AnySearchOrNone_NeverFindsASiteUser(string search)
    {
        var (client, token) = await StaffClientAsync();
        await SeedSiteUserAsync("سارا محمدی", "+989121234567");
        await CreateMemberAsync(client, token, "سارا رضایی", "09351114567");

        var result = await ListMembersAsync(client, token, search);

        result.Items.ShouldAllBe(member => member.FullName == "سارا رضایی");
    }

    [Theory]
    [InlineData("?isActive=true")]
    [InlineData("?isActive=false")]
    [InlineData("?debtorsOnly=true")]
    [InlineData("?planEndedOnly=true")]
    public async Task ListMembers_WithAFilter_NeverCountsASiteUser(string queryString)
    {
        var (client, token) = await StaffClientAsync();
        var siteUser = await SeedSiteUserAsync("سارا محمدی", "+989121234567");
        await ExecuteSqlAsync($"UPDATE members SET is_active = false WHERE id = '{siteUser.Id}'");

        var result = await GetAsync<PagedResponse<MemberResponse>>(client, token, MembersPath + queryString);

        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task ListMembers_DeskMemberWithNoPlan_IsListed()
    {
        // §16: a member the desk registered is never a site user, with or without a plan.
        var (client, token) = await StaffClientAsync();
        await CreateMemberAsync(client, token, "رضا احمدی", "09121234567");

        (await ListMembersAsync(client, token, "")).Items.ShouldHaveSingleItem().FullName.ShouldBe("رضا احمدی");
        (await ListSiteUsersAsync(client, token, "")).TotalCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(BroadcastAudience.ActiveMembers)]
    [InlineData(BroadcastAudience.MembersWithUsablePlan)]
    public async Task BroadcastRecipients_EitherGroup_LeavesSiteUsersOut(BroadcastAudience audience)
    {
        await SeedSiteUserAsync("سارا محمدی", "+989121234567");
        var deskMember = TestMembers.Seed("رضا احمدی", "+989351234567");
        await AddAsync(deskMember);

        await using var scope = Fixture.CreateScope();
        var recipients = await scope.ServiceProvider.GetRequiredService<BroadcastRecipients>()
            .FindAsync(audience, new DateOnly(2026, 11, 10), withoutMessageOf: null, TestContext.Current.CancellationToken);

        recipients.ShouldNotContain(recipient => recipient.FullName == "سارا محمدی");
        if (audience == BroadcastAudience.ActiveMembers)
        {
            recipients.ShouldHaveSingleItem().MemberId.ShouldBe(deskMember.Id);
        }
    }

    // ---- The «کاربران سایت» page ----

    [Fact]
    public async Task ListSiteUsers_SeveralSignUps_NewestFirstAndPaged()
    {
        var (client, token) = await StaffClientAsync();
        await SeedSiteUserAsync("اول", "+989121110001");
        await SeedSiteUserAsync("دوم", "+989121110002");
        await SeedSiteUserAsync("سوم", "+989121110003");

        var firstPage = await GetAsync<PagedResponse<SiteUserResponse>>(client, token, $"{SiteUsersPath}?page=1&pageSize=2");
        var secondPage = await GetAsync<PagedResponse<SiteUserResponse>>(client, token, $"{SiteUsersPath}?page=2&pageSize=2");

        firstPage.TotalCount.ShouldBe(3);
        firstPage.Items.Select(user => user.FullName).ShouldBe(["سوم", "دوم"]);
        secondPage.Items.Select(user => user.FullName).ShouldBe(["اول"]);
    }

    [Fact]
    public async Task ListSiteUsers_DeskMembers_AreNotOnIt()
    {
        var (client, token) = await StaffClientAsync();
        await SeedSiteUserAsync("سارا محمدی", "+989121234567");
        await CreateMemberAsync(client, token, "رضا احمدی", "09351234567");

        (await ListSiteUsersAsync(client, token, "")).Items.ShouldHaveSingleItem().FullName.ShouldBe("سارا محمدی");
    }

    [Theory]
    [InlineData("سارا", "سارا محمدی")]
    [InlineData("4567", "سارا محمدی")]
    [InlineData("0567", "مریم کریمی")]
    [InlineData("09121234567", "سارا محمدی")]
    [InlineData("+98 912 123 4567", "سارا محمدی")]
    public async Task ListSiteUsers_NameLastFourOrWholePhone_FindsThem(string search, string expected)
    {
        var (client, token) = await StaffClientAsync();
        await SeedSiteUserAsync("سارا محمدی", "+989121234567");
        await SeedSiteUserAsync("مریم کریمی", "+989351230567");

        var result = await ListSiteUsersAsync(client, token, search);

        result.Items.ShouldHaveSingleItem().FullName.ShouldBe(expected);
    }

    [Theory]
    [InlineData("0912")]
    [InlineData("1234")]
    [InlineData("234567")]
    public async Task ListSiteUsers_StartOrMiddleOfAPhone_FindsNothing(string search)
    {
        var (client, token) = await StaffClientAsync();
        await SeedSiteUserAsync("سارا محمدی", "+989121234567");

        (await ListSiteUsersAsync(client, token, search)).TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task ListSiteUsers_AsOwner_Returns200()
    {
        var (client, token) = await OwnerClientAsync();
        await SeedSiteUserAsync("سارا محمدی", "+989121234567");

        (await ListSiteUsersAsync(client, token, "")).TotalCount.ShouldBe(1);
    }

    // ---- The new-member form ----

    [Theory]
    [InlineData("09121234567")]
    [InlineData("۰۹۱۲۱۲۳۴۵۶۷")]
    [InlineData("+98 912 123 4567")]
    public async Task FindByPhone_ASiteUsersWholeNumber_ReturnsThem(string phoneNumber)
    {
        var (client, token) = await StaffClientAsync();
        var siteUser = await SeedSiteUserAsync("سارا محمدی", "+989121234567");

        var found = await FindByPhoneAsync(client, token, phoneNumber);

        found.SiteUser.ShouldNotBeNull().Id.ShouldBe(siteUser.Id);
        found.SiteUser.FullName.ShouldBe("سارا محمدی");
        found.SiteUser.BirthDate.ShouldBe(BirthDate);
    }

    [Theory]
    [InlineData("4567")]
    [InlineData("0912123")]
    [InlineData("")]
    [InlineData("not a phone")]
    public async Task FindByPhone_PartOfANumberOrNothing_ReturnsNobody(string phoneNumber)
    {
        var (client, token) = await StaffClientAsync();
        await SeedSiteUserAsync("سارا محمدی", "+989121234567");

        (await FindByPhoneAsync(client, token, phoneNumber)).SiteUser.ShouldBeNull();
    }

    [Fact]
    public async Task FindByPhone_ADeskMembersNumber_ReturnsNobody()
    {
        var (client, token) = await StaffClientAsync();
        await CreateMemberAsync(client, token, "رضا احمدی", "09121234567");

        (await FindByPhoneAsync(client, token, "09121234567")).SiteUser.ShouldBeNull();
    }

    [Fact]
    public async Task CreateMember_ASiteUsersPhone_Returns409PhoneBelongsToSiteUser()
    {
        var (client, token) = await StaffClientAsync();
        await SeedSiteUserAsync("سارا محمدی", "+989121234567");

        using var response = await PostAsync(
            client, token, MembersPath, new { fullName = "سارا", phoneNumber = "09121234567", birthDate = "1995-04-20" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadJsonAsync()).GetProperty("code").GetString().ShouldBe(MemberErrors.PhoneBelongsToSiteUser.Code);
    }

    [Fact]
    public async Task CreateMember_ADeskMembersPhone_StillReturnsPhoneAlreadyExists()
    {
        var (client, token) = await StaffClientAsync();
        await CreateMemberAsync(client, token, "رضا احمدی", "09121234567");

        using var response = await PostAsync(
            client, token, MembersPath, new { fullName = "رضا", phoneNumber = "09121234567", birthDate = "1990-06-15" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadJsonAsync()).GetProperty("code").GetString().ShouldBe(MemberErrors.PhoneAlreadyExists.Code);
    }

    [Fact]
    public async Task Register_ASiteUser_BecomesAMemberWithWhatTheyEnteredAndLeavesThePage()
    {
        var (client, token) = await StaffClientAsync();
        var siteUser = await SeedSiteUserAsync("سارا محمدی", "+989121234567");

        using var response = await PostAsync(client, token, $"{SiteUsersPath}/{siteUser.Id}/register", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var member = (await response.Content.ReadFromJsonAsync<MemberResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        member.Id.ShouldBe(siteUser.Id);
        member.FullName.ShouldBe("سارا محمدی");
        member.BirthDate.ShouldBe(BirthDate);
        (await ListMembersAsync(client, token, "4567")).Items.ShouldHaveSingleItem().Id.ShouldBe(siteUser.Id);
        (await ListSiteUsersAsync(client, token, "")).TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Register_ADeskMember_Returns404SiteUserNotFound()
    {
        var (client, token) = await StaffClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا احمدی", "09121234567");

        using var response = await PostAsync(client, token, $"{SiteUsersPath}/{member.Id}/register", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadJsonAsync()).GetProperty("code").GetString().ShouldBe(MemberErrors.SiteUserNotFound.Code);
    }

    // ---- Becoming a member ----

    [Fact]
    public async Task SellPlan_ToASiteUser_MakesThemAMemberForGoodEvenIfCancelled()
    {
        var (client, token) = await OwnerClientAsync();
        var siteUser = await SeedSiteUserAsync("سارا محمدی", "+989121234567");
        var plan = await TestPlans.AddAsync(Fixture, sessions: 10, price: 1_000_000m);

        using var sold = await PostAsync(client, token, $"{MembersPath}/{siteUser.Id}/subscriptions", plan.Body);
        sold.StatusCode.ShouldBe(HttpStatusCode.Created);

        (await ListMembersAsync(client, token, "")).Items.ShouldHaveSingleItem().Id.ShouldBe(siteUser.Id);
        (await ListSiteUsersAsync(client, token, "")).TotalCount.ShouldBe(0);

        // §16, the developer: a cancelled plan does not turn them back into a site user.
        await ExecuteSqlAsync($"UPDATE subscriptions SET cancelled_at = now(), cancellation_reason = 'test' WHERE member_id = '{siteUser.Id}'");
        (await ListSiteUsersAsync(client, token, "")).TotalCount.ShouldBe(0);
        (await ListMembersAsync(client, token, "")).TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task CafeOrder_OnASiteUsersName_MakesThemAMemberWithTheirDebt()
    {
        // §16, the developer: someone who bought at the cafe has been to the gym. Before 16.1 the
        // cafe's search could find a site sign-up, so such orders are on the server already.
        var (client, token) = await StaffClientAsync();
        var siteUser = await SeedSiteUserAsync("سارا محمدی", "+989121234567");
        await using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var staffId = await db.Users.Where(user => user.UserName == "staff").Select(user => user.Id)
                .SingleAsync(TestContext.Current.CancellationToken);
            var category = ProductCategory.Create("نوشیدنی").Value;
            var product = Product.Create("آب معدنی", category.Id, 20_000m).Value;
            db.ProductCategories.Add(category);
            db.Products.Add(product);
            db.CafeOrders.Add(CafeOrder.Create(siteUser.Id, [(product, 2)], new DateOnly(2026, 11, 1), staffId).Value);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        (await ListSiteUsersAsync(client, token, "")).TotalCount.ShouldBe(0);
        var debtor = (await GetAsync<PagedResponse<MemberResponse>>(client, token, $"{MembersPath}?debtorsOnly=true"))
            .Items.ShouldHaveSingleItem();
        debtor.Id.ShouldBe(siteUser.Id);
        debtor.Debt.ShouldBe(40_000m);
    }

    [Fact]
    public async Task CardioOnlyCheckIn_ASiteUser_MakesThemAMember()
    {
        // A visit with no plan at all: the cardio-only check-in sells nothing.
        var (client, token) = await StaffClientAsync();
        var siteUser = await SeedSiteUserAsync("سارا محمدی", "+989121234567");

        using var visit = await PostAsync(
            client, token, $"{MembersPath}/{siteUser.Id}/attendance/cardio-only-check-in", new { lockerId = TestLockers.IdOf(1) });
        visit.StatusCode.ShouldBe(HttpStatusCode.Created);

        (await ListMembersAsync(client, token, "")).Items.ShouldHaveSingleItem().Id.ShouldBe(siteUser.Id);
        (await ListSiteUsersAsync(client, token, "")).TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task SiteUserRule_SignUpThatAlreadyHadAPlan_IsNotASiteUser()
    {
        // The migration marks every site sign-up, including those who bought a plan since: the
        // plan alone keeps them a member.
        var (client, token) = await OwnerClientAsync();
        var siteUser = await SeedSiteUserAsync("سارا محمدی", "+989121234567");
        await SellSingleVisitAsync(client, token, siteUser.Id);

        (await ListSiteUsersAsync(client, token, "")).TotalCount.ShouldBe(0);
        (await ListMembersAsync(client, token, "سارا")).TotalCount.ShouldBe(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/by-phone?phoneNumber=09121234567")]
    public async Task SiteUsers_WithoutSigningIn_Returns401(string path)
    {
        using var client = Fixture.CreateClient();

        using var response = await client.GetAsync(SiteUsersPath + path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_WithoutSigningIn_Returns401()
    {
        var siteUser = await SeedSiteUserAsync("سارا محمدی", "+989121234567");
        using var client = Fixture.CreateClient();

        using var response = await client.PostAsync($"{SiteUsersPath}/{siteUser.Id}/register", content: null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Helpers ----

    private async Task<Member> SeedSiteUserAsync(string fullName, string phoneNumber)
    {
        var member = Member.SignUpOnSite(fullName, phoneNumber, BirthDate, new DateOnly(2026, 11, 10)).Value;

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        db.SiteAccounts.Add(SiteAccount.ForMember(member.Id, TimeProvider.System.GetUtcNow()));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    private async Task AddAsync(Member member)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<(HttpClient Client, string Token)> StaffClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    private async Task<(HttpClient Client, string Token)> OwnerClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("owner", TestUsers.Password));
    }

    private static async Task<MemberResponse> CreateMemberAsync(HttpClient client, string token, string fullName, string phoneNumber)
    {
        using var response = await PostAsync(client, token, MembersPath, new { fullName, phoneNumber, birthDate = "1990-06-15" });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<MemberResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private async Task SellSingleVisitAsync(HttpClient client, string token, Guid memberId)
    {
        await TestPlans.SetPricesAsync(Fixture, singleVisitPrice: 150_000m);
        using var response = await PostAsync(client, token, $"{MembersPath}/{memberId}/subscriptions/single-visit", body: null);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static Task<PagedResponse<MemberResponse>> ListMembersAsync(HttpClient client, string token, string search) =>
        GetAsync<PagedResponse<MemberResponse>>(client, token, $"{MembersPath}?search={Uri.EscapeDataString(search)}");

    private static Task<PagedResponse<SiteUserResponse>> ListSiteUsersAsync(HttpClient client, string token, string search) =>
        GetAsync<PagedResponse<SiteUserResponse>>(client, token, $"{SiteUsersPath}?search={Uri.EscapeDataString(search)}");

    private static Task<SiteUserByPhoneResponse> FindByPhoneAsync(HttpClient client, string token, string phoneNumber) =>
        GetAsync<SiteUserByPhoneResponse>(client, token, $"{SiteUsersPath}/by-phone?phoneNumber={Uri.EscapeDataString(phoneNumber)}");

    private static async Task<T> GetAsync<T>(HttpClient client, string token, string path)
        where T : class
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string token, string path, object? body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database
            .ExecuteSqlRawAsync(sql, TestContext.Current.CancellationToken);
    }
}
