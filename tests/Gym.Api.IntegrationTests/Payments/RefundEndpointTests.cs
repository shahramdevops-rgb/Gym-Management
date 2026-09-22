using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common;
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
/// Refunds: <c>POST /api/subscriptions/{id}/refunds</c> (BUSINESS_RULES.md §5). Owner only
/// (task 4.5). A "void" is just a full refund, so it is not a separate endpoint.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class RefundEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static int _phoneSuffix;

    [Fact]
    public async Task Refund_Partial_LowersNetPaidAndRecalculatesStatusToPartial()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var sold = await SellSubscriptionAsync(staffClient, staffToken, 900_000m);
        await RegisterPaymentAsync(staffClient, staffToken, sold.Id, 900_000m);

        using var response = await RefundAsync(ownerClient, ownerToken, sold.Id, 300_000m, "بازگشت جزئی وجه");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var refund = await ReadAsync(response);
        refund.Kind.ShouldBe(PaymentKind.Refund);
        refund.Amount.ShouldBe(300_000m);
        refund.Reason.ShouldBe("بازگشت جزئی وجه");
        refund.SubscriptionNetPaid.ShouldBe(600_000m);
        refund.SubscriptionPaymentStatus.ShouldBe(PaymentStatus.Partial);
    }

    [Fact]
    public async Task Refund_FullAmountOfAMistakenPayment_IsAVoidThatRecalculatesToUnpaid()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var sold = await SellSubscriptionAsync(staffClient, staffToken, 900_000m);
        await RegisterPaymentAsync(staffClient, staffToken, sold.Id, 900_000m);

        using var response = await RefundAsync(ownerClient, ownerToken, sold.Id, 900_000m, "اشتباه در ثبت مبلغ");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var refund = await ReadAsync(response);
        refund.SubscriptionNetPaid.ShouldBe(0m);
        refund.SubscriptionPaymentStatus.ShouldBe(PaymentStatus.Unpaid);
    }

    [Fact]
    public async Task Refund_ExceedingNetPaid_Returns422RefundExceedsNetPaid()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var sold = await SellSubscriptionAsync(staffClient, staffToken, 900_000m);
        await RegisterPaymentAsync(staffClient, staffToken, sold.Id, 400_000m);

        using var response = await RefundAsync(ownerClient, ownerToken, sold.Id, 500_000m, "دلیل");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Payments.RefundExceedsNetPaid");
    }

    [Fact]
    public async Task Refund_WithNoPayments_Returns422RefundExceedsNetPaid()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var sold = await SellSubscriptionAsync(staffClient, staffToken, 900_000m);

        using var response = await RefundAsync(ownerClient, ownerToken, sold.Id, 1m, "دلیل");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Payments.RefundExceedsNetPaid");
    }

    [Fact]
    public async Task Refund_BlankReason_Returns400WithFieldCode()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var sold = await SellSubscriptionAsync(staffClient, staffToken, 900_000m);
        await RegisterPaymentAsync(staffClient, staffToken, sold.Id, 900_000m);

        using var response = await RefundAsync(ownerClient, ownerToken, sold.Id, 100_000m, "");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("errors").GetProperty("reason")[0].GetProperty("code").GetString()
            .ShouldBe("Payments.RefundReasonRequired");
    }

    [Fact]
    public async Task Refund_ByStaff_Returns403()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var sold = await SellSubscriptionAsync(staffClient, staffToken, 900_000m);
        await RegisterPaymentAsync(staffClient, staffToken, sold.Id, 900_000m);

        using var response = await RefundAsync(staffClient, staffToken, sold.Id, 100_000m, "دلیل");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Refund_UnknownSubscription_Returns404()
    {
        var (ownerClient, ownerToken) = await OwnerClientAsync();

        using var response = await RefundAsync(ownerClient, ownerToken, Guid.CreateVersion7(), 100m, "دلیل");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Refund_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/subscriptions/{Guid.CreateVersion7()}/refunds",
            new { amount = 100m, method = "Cash", reason = "دلیل" },
            TestContext.Current.CancellationToken);

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

    private async Task<Plan> AddPlanAsync(decimal price)
    {
        var plan = Plan.Create("پلن", 30, 12, price).Value;

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Plans.Add(plan);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return plan;
    }

    private async Task<SubscriptionResponse> SellSubscriptionAsync(HttpClient client, string token, decimal price)
    {
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync(price);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/members/{member.Id}/subscriptions")
        {
            Content = JsonContent.Create(new { planId = plan.Id }),
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

    private static Task<HttpResponseMessage> RefundAsync(
        HttpClient client, string token, Guid subscriptionId, decimal amount, string reason)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/subscriptions/{subscriptionId}/refunds")
        {
            Content = JsonContent.Create(new { amount, method = "Cash", reason }),
        };

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }

    private static async Task<PaymentResponse> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<PaymentResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
}
