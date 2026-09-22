using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Members.GetMemberDebt;
using Gym.Application.Subscriptions;
using Gym.Domain.Members;
using Gym.Domain.Plans;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Members;

/// <summary>
/// <c>GET /api/members/{id}/debt</c>: what a member still owes, item by item
/// (BUSINESS_RULES.md §5 <i>Member debt</i>, task 4.7). Front-desk work, so both roles.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class MemberDebtEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static int _phoneSuffix;
    private static int _planSuffix;

    [Fact]
    public async Task GetDebt_MemberWithNoSubscription_IsZeroWithNoItems()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();

        var debt = await GetDebtOkAsync(client, token, member.Id);

        debt.Total.ShouldBe(0m);
        debt.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetDebt_UnpaidSubscription_OwesItsWholePrice()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var sold = await SellAsync(client, token, member.Id, 900_000m, planName: "پلن طلایی");

        var debt = await GetDebtOkAsync(client, token, member.Id);

        debt.Total.ShouldBe(900_000m);
        var item = debt.Items.ShouldHaveSingleItem();
        item.SubscriptionId.ShouldBe(sold.Id);
        item.PlanName.ShouldBe("پلن طلایی");
        item.StartDate.ShouldBe(sold.StartDate);
        item.EndDate.ShouldBe(sold.EndDate);
        item.Price.ShouldBe(900_000m);
        item.NetPaid.ShouldBe(0m);
        item.Outstanding.ShouldBe(900_000m);
    }

    [Fact]
    public async Task GetDebt_PartiallyPaidSubscription_OwesTheRemainder()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var sold = await SellAsync(client, token, member.Id, 900_000m);
        await PayAsync(client, token, sold.Id, 300_000m);

        var debt = await GetDebtOkAsync(client, token, member.Id);

        debt.Total.ShouldBe(600_000m);
        var item = debt.Items.ShouldHaveSingleItem();
        item.NetPaid.ShouldBe(300_000m);
        item.Outstanding.ShouldBe(600_000m);
    }

    [Fact]
    public async Task GetDebt_FullyPaidSubscription_IsZeroAndNotInTheBreakdown()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var sold = await SellAsync(client, token, member.Id, 900_000m);
        await PayAsync(client, token, sold.Id, 900_000m);

        var debt = await GetDebtOkAsync(client, token, member.Id);

        debt.Total.ShouldBe(0m);
        debt.Items.ShouldBeEmpty();
    }

    /// <summary>
    /// The total is the sum of the named items and nothing else (BUSINESS_RULES.md §5): three
    /// sales, each owing a different amount, add up and each appears once.
    /// </summary>
    [Fact]
    public async Task GetDebt_SeveralSubscriptions_AddsThemUpItemByItem()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var first = await SellAsync(client, token, member.Id, 900_000m);
        await PayAsync(client, token, first.Id, 400_000m);
        var second = await SellAsync(client, token, member.Id, 500_000m);
        var third = await SellAsync(client, token, member.Id, 300_000m);
        await PayAsync(client, token, third.Id, 300_000m);

        var debt = await GetDebtOkAsync(client, token, member.Id);

        debt.Total.ShouldBe(1_000_000m);
        debt.Items.Count.ShouldBe(2);
        debt.Items.Single(item => item.SubscriptionId == first.Id).Outstanding.ShouldBe(500_000m);
        debt.Items.Single(item => item.SubscriptionId == second.Id).Outstanding.ShouldBe(500_000m);
        debt.Items.ShouldNotContain(item => item.SubscriptionId == third.Id);
        debt.Total.ShouldBe(debt.Items.Sum(item => item.Outstanding));
    }

    /// <summary>
    /// Cancelling is how the gym says it is not chasing that money (BUSINESS_RULES.md §5), so a
    /// cancelled, never-paid subscription owes nothing and leaves the breakdown.
    /// </summary>
    [Fact]
    public async Task GetDebt_CancelledSubscription_IsIgnored()
    {
        var (client, token) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var kept = await SellAsync(client, token, member.Id, 900_000m);
        var cancelled = await SellAsync(client, token, member.Id, 500_000m);
        using var cancelResponse = await SendAsync(
            ownerClient, ownerToken, $"/api/subscriptions/{cancelled.Id}/cancel", new { reason = "اشتباه ثبت شد" });
        cancelResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var debt = await GetDebtOkAsync(client, token, member.Id);

        debt.Total.ShouldBe(900_000m);
        debt.Items.ShouldHaveSingleItem().SubscriptionId.ShouldBe(kept.Id);
    }

    /// <summary>A free item owes nothing and never appears in the breakdown (BUSINESS_RULES.md §5).</summary>
    [Fact]
    public async Task GetDebt_FreeSubscription_IsNotInTheBreakdown()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        await SellAsync(client, token, member.Id, 0m);

        var debt = await GetDebtOkAsync(client, token, member.Id);

        debt.Total.ShouldBe(0m);
        debt.Items.ShouldBeEmpty();
    }

    /// <summary>A refund puts the money back on the member's account as debt again.</summary>
    [Fact]
    public async Task GetDebt_AfterARefund_OwesTheRefundedAmountAgain()
    {
        var (client, token) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var sold = await SellAsync(client, token, member.Id, 900_000m);
        await PayAsync(client, token, sold.Id, 900_000m);
        using var refund = await SendAsync(
            ownerClient, ownerToken, $"/api/subscriptions/{sold.Id}/refunds",
            new { amount = 200_000m, method = "Cash", reason = "بازگشت وجه" });
        refund.StatusCode.ShouldBe(HttpStatusCode.Created);

        var debt = await GetDebtOkAsync(client, token, member.Id);

        debt.Total.ShouldBe(200_000m);
        debt.Items.ShouldHaveSingleItem().NetPaid.ShouldBe(700_000m);
    }

    /// <summary>Another member's sale never reaches this member's total.</summary>
    [Fact]
    public async Task GetDebt_AnotherMembersDebt_IsNotCounted()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var other = await AddMemberAsync();
        await SellAsync(client, token, other.Id, 900_000m);

        var debt = await GetDebtOkAsync(client, token, member.Id);

        debt.Total.ShouldBe(0m);
        debt.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetDebt_ByOwner_Succeeds()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        await SellAsync(staffClient, staffToken, member.Id, 900_000m);

        var debt = await GetDebtOkAsync(ownerClient, ownerToken, member.Id);

        debt.Total.ShouldBe(900_000m);
    }

    [Fact]
    public async Task GetDebt_UnknownMember_Returns404()
    {
        var (client, token) = await StaffClientAsync();

        using var response = await GetDebtAsync(client, token, Guid.CreateVersion7());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDebt_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.GetAsync(
            new Uri($"/api/members/{Guid.CreateVersion7()}/debt", UriKind.Relative), TestContext.Current.CancellationToken);

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
        var member = TestMembers.Seed("رضا احمدی", $"+98912{suffix:D7}");

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    /// <summary>Plan names are unique, and one test sells three plans to the same member.</summary>
    private async Task<Plan> AddPlanAsync(decimal price, string? name)
    {
        var plan = Plan.Create(name ?? $"پلن {Interlocked.Increment(ref _planSuffix)}", 30, 12, price).Value;

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Plans.Add(plan);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return plan;
    }

    /// <summary>
    /// Sold through the real endpoint, so the price is a plan's saved snapshot and the second and
    /// third sales are queued behind the first exactly as the gym would have them.
    /// </summary>
    private async Task<SubscriptionResponse> SellAsync(
        HttpClient client, string token, Guid memberId, decimal price, string? planName = null)
    {
        var plan = await AddPlanAsync(price, planName);

        using var response = await SendAsync(client, token, $"/api/members/{memberId}/subscriptions", new { planId = plan.Id });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<SubscriptionResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task PayAsync(HttpClient client, string token, Guid subscriptionId, decimal amount)
    {
        using var response = await SendAsync(
            client, token, $"/api/subscriptions/{subscriptionId}/payments", new { amount, method = "Cash" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static Task<HttpResponseMessage> GetDebtAsync(HttpClient client, string token, Guid memberId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/members/{memberId}/debt");

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }

    private static async Task<MemberDebtResponse> GetDebtOkAsync(HttpClient client, string token, Guid memberId)
    {
        using var response = await GetDebtAsync(client, token, memberId);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<MemberDebtResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string token, string path, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }
}
