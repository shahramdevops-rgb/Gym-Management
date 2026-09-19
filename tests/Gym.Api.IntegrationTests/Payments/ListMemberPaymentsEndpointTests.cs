using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common.Paging;
using Gym.Application.Payments;
using Gym.Application.Subscriptions;
using Gym.Domain.Members;
using Gym.Domain.Payments;
using Gym.Domain.Plans;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Payments;

/// <summary>
/// A member's payment history across all of their subscriptions:
/// <c>GET /api/members/{id}/payments</c> (task 4.5).
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class ListMemberPaymentsEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static int _phoneSuffix;

    [Fact]
    public async Task List_PaymentsAcrossTwoSubscriptions_ReturnsAllOfThemIncludingTheRefund()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var planA = await AddPlanAsync("پلن یک", 900_000m);
        var planB = await AddPlanAsync("پلن دو", 500_000m);
        var subscriptionA = await AssignOkAsync(staffClient, staffToken, member.Id, planA.Id);
        await RegisterPaymentAsync(staffClient, staffToken, subscriptionA.Id, 900_000m);
        var subscriptionB = await AssignOkAsync(staffClient, staffToken, member.Id, planB.Id);
        await RegisterPaymentAsync(staffClient, staffToken, subscriptionB.Id, 200_000m);
        await RefundAsync(ownerClient, ownerToken, subscriptionA.Id, 100_000m, "بازگشت جزئی");

        var page = await ListOkAsync(staffClient, staffToken, member.Id);

        page.TotalCount.ShouldBe(3);
        page.Items.ShouldContain(item => item.SubscriptionId == subscriptionA.Id && item.Kind == PaymentKind.Payment && item.Amount == 900_000m);
        page.Items.ShouldContain(item => item.SubscriptionId == subscriptionA.Id && item.Kind == PaymentKind.Refund && item.Amount == 100_000m);
        page.Items.ShouldContain(item => item.SubscriptionId == subscriptionB.Id && item.Kind == PaymentKind.Payment && item.Amount == 200_000m);
    }

    [Fact]
    public async Task List_MemberWithNoPayments_ReturnsAnEmptyPage()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();

        var page = await ListOkAsync(client, token, member.Id);

        page.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task List_UnknownMember_Returns404()
    {
        var (client, token) = await StaffClientAsync();

        using var response = await SendAsync(client, token, Guid.CreateVersion7());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.GetAsync(
            $"/api/members/{Guid.CreateVersion7()}/payments", TestContext.Current.CancellationToken);

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

    private async Task<Plan> AddPlanAsync(string name, decimal price)
    {
        var plan = Plan.Create(name, 30, 12, price).Value;

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

    private static async Task RegisterPaymentAsync(HttpClient client, string token, Guid subscriptionId, decimal amount)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/subscriptions/{subscriptionId}/payments")
        {
            Content = JsonContent.Create(new { amount, method = "Cash" }),
        };
        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static async Task RefundAsync(HttpClient client, string token, Guid subscriptionId, decimal amount, string reason)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/subscriptions/{subscriptionId}/refunds")
        {
            Content = JsonContent.Create(new { amount, method = "Cash", reason }),
        };
        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string token, Guid memberId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/members/{memberId}/payments");

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }

    private static async Task<PagedResponse<PaymentHistoryResponse>> ListOkAsync(HttpClient client, string token, Guid memberId)
    {
        using var response = await SendAsync(client, token, memberId);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<PagedResponse<PaymentHistoryResponse>>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull();
    }
}
