using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common.Paging;
using Gym.Application.Subscriptions;
using Gym.Domain.Members;
using Gym.Domain.Plans;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Subscriptions;

/// <summary>
/// A member's subscription history: <c>GET /api/members/{id}/subscriptions</c> (task 4.5).
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class ListMemberSubscriptionsEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static int _phoneSuffix;

    [Fact]
    public async Task List_SeveralSubscriptions_ReturnsThemNewestFirst()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync("ماهانه", 30, null, 900_000m);
        var first = await AssignOkAsync(client, token, member.Id, plan.Id);
        var second = await AssignOkAsync(client, token, member.Id, plan.Id);

        var page = await ListOkAsync(client, token, member.Id);

        page.TotalCount.ShouldBe(2);
        page.Items[0].Id.ShouldBe(second.Id);
        page.Items[1].Id.ShouldBe(first.Id);
    }

    [Fact]
    public async Task List_MemberWithNoSubscriptions_ReturnsAnEmptyPage()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();

        var page = await ListOkAsync(client, token, member.Id);

        page.TotalCount.ShouldBe(0);
        page.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task List_UnknownMember_Returns404()
    {
        var (client, token) = await StaffClientAsync();

        using var response = await SendAsync(client, token, Guid.CreateVersion7());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_ByStaff_Succeeds()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();

        using var response = await SendAsync(client, token, member.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task List_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.GetAsync(
            $"/api/members/{Guid.CreateVersion7()}/subscriptions", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Helpers ----

    private async Task<(HttpClient Client, string Token)> StaffClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    private async Task<Member> AddMemberAsync()
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = Member.Create("رضا احمدی", $"+98912{suffix:D7}", null).Value;

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    private async Task<Plan> AddPlanAsync(string name, int durationDays, int? sessionCount, decimal price)
    {
        var plan = Plan.Create(name, durationDays, sessionCount, price).Value;

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Plans.Add(plan);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return plan;
    }

    private static async Task<SubscriptionResponse> AssignOkAsync(HttpClient client, string token, Guid memberId, Guid planId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/members/{memberId}/subscriptions")
        {
            Content = JsonContent.Create(new { planId }),
        };
        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<SubscriptionResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string token, Guid memberId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/members/{memberId}/subscriptions");

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }

    private static async Task<PagedResponse<SubscriptionResponse>> ListOkAsync(HttpClient client, string token, Guid memberId)
    {
        using var response = await SendAsync(client, token, memberId);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<PagedResponse<SubscriptionResponse>>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull();
    }
}
