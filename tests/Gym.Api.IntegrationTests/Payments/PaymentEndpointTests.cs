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
/// Registering payments: <c>POST /api/subscriptions/{id}/payments</c> (BUSINESS_RULES.md §5).
/// Both roles (task 4.4). Refunds and payment history are task 4.5.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class PaymentEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static int _phoneSuffix;

    [Fact]
    public async Task Register_PartialAmount_ReturnsPartialStatus()
    {
        var (client, token) = await StaffClientAsync();
        var sold = await SellSubscriptionAsync(client, token, 900_000m);

        using var response = await RegisterAsync(client, token, sold.Id, 400_000m, PaymentMethod.Cash, null);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payment = await ReadAsync(response);
        payment.SubscriptionId.ShouldBe(sold.Id);
        payment.Kind.ShouldBe(PaymentKind.Payment);
        payment.Amount.ShouldBe(400_000m);
        payment.Method.ShouldBe(PaymentMethod.Cash);
        payment.SubscriptionNetPaid.ShouldBe(400_000m);
        payment.SubscriptionPaymentStatus.ShouldBe(PaymentStatus.Partial);
    }

    [Fact]
    public async Task Register_FullAmount_ReturnsPaidStatus()
    {
        var (client, token) = await StaffClientAsync();
        var sold = await SellSubscriptionAsync(client, token, 900_000m);

        using var response = await RegisterAsync(client, token, sold.Id, 900_000m, PaymentMethod.Card, "ref-1");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payment = await ReadAsync(response);
        payment.ReferenceNumber.ShouldBe("ref-1");
        payment.SubscriptionNetPaid.ShouldBe(900_000m);
        payment.SubscriptionPaymentStatus.ShouldBe(PaymentStatus.Paid);
    }

    [Fact]
    public async Task Register_TwoPartialPayments_AccumulateToPaid()
    {
        var (client, token) = await StaffClientAsync();
        var sold = await SellSubscriptionAsync(client, token, 900_000m);

        var first = await RegisterOkAsync(client, token, sold.Id, 300_000m, PaymentMethod.Cash, null);
        first.SubscriptionPaymentStatus.ShouldBe(PaymentStatus.Partial);

        var second = await RegisterOkAsync(client, token, sold.Id, 600_000m, PaymentMethod.BankTransfer, null);
        second.SubscriptionNetPaid.ShouldBe(900_000m);
        second.SubscriptionPaymentStatus.ShouldBe(PaymentStatus.Paid);
    }

    [Fact]
    public async Task Register_AmountExceedingThePrice_Returns422Overpayment()
    {
        var (client, token) = await StaffClientAsync();
        var sold = await SellSubscriptionAsync(client, token, 900_000m);

        using var response = await RegisterAsync(client, token, sold.Id, 950_000m, PaymentMethod.Cash, null);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Payments.Overpayment");
    }

    [Fact]
    public async Task Register_AmountExceedingTheRemainingBalance_Returns422Overpayment()
    {
        var (client, token) = await StaffClientAsync();
        var sold = await SellSubscriptionAsync(client, token, 900_000m);
        await RegisterOkAsync(client, token, sold.Id, 500_000m, PaymentMethod.Cash, null);

        using var response = await RegisterAsync(client, token, sold.Id, 500_000m, PaymentMethod.Cash, null);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Payments.Overpayment");
    }

    [Fact]
    public async Task Register_ZeroAmount_Returns400WithFieldCode()
    {
        var (client, token) = await StaffClientAsync();
        var sold = await SellSubscriptionAsync(client, token, 900_000m);

        using var response = await RegisterAsync(client, token, sold.Id, 0m, PaymentMethod.Cash, null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("errors").GetProperty("amount")[0].GetProperty("code").GetString()
            .ShouldBe("Payments.AmountNotPositive");
    }

    [Fact]
    public async Task Register_UnknownSubscription_Returns404()
    {
        var (client, token) = await StaffClientAsync();

        using var response = await RegisterAsync(client, token, Guid.CreateVersion7(), 100m, PaymentMethod.Cash, null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Register_ByOwner_Succeeds()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var sold = await SellSubscriptionAsync(staffClient, staffToken, 900_000m);

        using var response = await RegisterAsync(ownerClient, ownerToken, sold.Id, 900_000m, PaymentMethod.Cash, null);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Register_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/subscriptions/{Guid.CreateVersion7()}/payments",
            new { amount = 100m, method = "Cash" },
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

    /// <summary>Assigns a fresh subscription through the real endpoint, so its price is a plan's real, saved snapshot.</summary>
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

    private static Task<HttpResponseMessage> RegisterAsync(
        HttpClient client, string token, Guid subscriptionId, decimal amount, PaymentMethod method, string? referenceNumber)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/subscriptions/{subscriptionId}/payments")
        {
            Content = JsonContent.Create(new { amount, method = method.ToString(), referenceNumber }),
        };

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }

    private static async Task<PaymentResponse> RegisterOkAsync(
        HttpClient client, string token, Guid subscriptionId, decimal amount, PaymentMethod method, string? referenceNumber)
    {
        using var response = await RegisterAsync(client, token, subscriptionId, amount, method, referenceNumber);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await ReadAsync(response);
    }

    private static async Task<PaymentResponse> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<PaymentResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
}
