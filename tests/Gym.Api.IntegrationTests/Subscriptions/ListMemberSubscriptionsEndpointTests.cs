using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common.Paging;
using Gym.Application.Subscriptions;
using Gym.Domain.Members;
using Gym.Domain.Payments;
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
    public async Task List_TwoSubscriptionsWithDifferentPayments_ReportsNetPaidPerRow()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var planA = await AddPlanAsync("پلن یک", 30, null, 900_000m);
        var planB = await AddPlanAsync("پلن دو", 30, null, 500_000m);
        var first = await AssignOkAsync(client, token, member.Id, planA.Id);
        await PayAsync(client, token, first.Id, 900_000m);
        var second = await AssignOkAsync(client, token, member.Id, planB.Id);
        await PayAsync(client, token, second.Id, 200_000m);

        var page = await ListOkAsync(client, token, member.Id);

        var firstRow = page.Items.Single(item => item.Id == first.Id);
        firstRow.NetPaid.ShouldBe(900_000m);
        firstRow.PaymentStatus.ShouldBe(PaymentStatus.Paid);
        var secondRow = page.Items.Single(item => item.Id == second.Id);
        secondRow.NetPaid.ShouldBe(200_000m);
        secondRow.PaymentStatus.ShouldBe(PaymentStatus.Partial);
    }

    [Fact]
    public async Task List_CancelledThenRenewedTheSameDay_PutsTheNewOneFirstNotTheCancelledOne()
    {
        // A cancelled subscription covers no dates (BUSINESS_RULES.md §4), so renewing it the
        // same day gives the new one the same StartDate as the cancelled one. StartDate alone
        // can no longer tell them apart; found manually testing task 4.6's UI, where the
        // member profile showed the cancelled subscription as "current" instead of the new one.
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync("ماهانه", 30, null, 900_000m);
        var cancelled = await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);
        await CancelAsync(ownerClient, ownerToken, cancelled.Id, "انصراف عضو");

        var renewed = await RenewOkAsync(staffClient, staffToken, member.Id);

        renewed.StartDate.ShouldBe(cancelled.StartDate);
        var page = await ListOkAsync(staffClient, staffToken, member.Id);
        page.Items[0].Id.ShouldBe(renewed.Id);
        page.Items[1].Id.ShouldBe(cancelled.Id);
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

    private async Task<(HttpClient Client, string Token)> OwnerClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("owner", TestUsers.Password));
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

    private static async Task CancelAsync(HttpClient client, string token, Guid subscriptionId, string reason)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/subscriptions/{subscriptionId}/cancel")
        {
            Content = JsonContent.Create(new { reason }),
        };
        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<SubscriptionResponse> RenewOkAsync(HttpClient client, string token, Guid memberId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/members/{memberId}/subscriptions/renew");
        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<SubscriptionResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task PayAsync(HttpClient client, string token, Guid subscriptionId, decimal amount)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/subscriptions/{subscriptionId}/payments")
        {
            Content = JsonContent.Create(new { amount, method = "Cash" }),
        };
        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
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
