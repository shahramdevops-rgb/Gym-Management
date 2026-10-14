using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Subscriptions;
using Gym.Domain.Members;
using Gym.Domain.Payments;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Subscriptions;

/// <summary>
/// Cancelling a subscription gives back what was paid on it, in the same step
/// (BUSINESS_RULES.md §4 Cancel, §5, roadmap 6.5.37): one refund per method, with the reason, and
/// no other way to refund a subscription.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class CancelSubscriptionRefundEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string Reason = "پلن اشتباهی فروخته شد";

    private static int _phoneSuffix;

    [Fact]
    public async Task Cancel_NothingPaid_CancelsAndRefundsNothing()
    {
        var (owner, ownerToken) = await OwnerClientAsync();
        var sold = await SellAsync(owner, ownerToken);

        var cancelled = await CancelOkAsync(owner, ownerToken, sold.Id);

        cancelled.NetPaid.ShouldBe(0m);
        (await StoredPaymentsAsync(sold.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Cancel_PaidInFull_RefundsTheWholeAmountWithTheReason()
    {
        var (owner, ownerToken) = await OwnerClientAsync();
        var sold = await SellAsync(owner, ownerToken);
        await PayOkAsync(owner, ownerToken, sold.Id, 900_000m, "Card");

        var cancelled = await CancelOkAsync(owner, ownerToken, sold.Id);

        cancelled.NetPaid.ShouldBe(0m);
        var refund = (await StoredPaymentsAsync(sold.Id)).Where(payment => payment.Kind == PaymentKind.Refund).ShouldHaveSingleItem();
        refund.Amount.ShouldBe(900_000m);
        refund.Method.ShouldBe(PaymentMethod.Card);
        refund.Reason.ShouldBe(Reason);
    }

    /// <summary>Money goes back the way it came: cash as cash, a card payment on the card.</summary>
    [Fact]
    public async Task Cancel_PaidInCashAndByCard_RefundsEachMethodSeparately()
    {
        var (owner, ownerToken) = await OwnerClientAsync();
        var sold = await SellAsync(owner, ownerToken);
        await PayOkAsync(owner, ownerToken, sold.Id, 600_000m, "Card");
        await PayOkAsync(owner, ownerToken, sold.Id, 200_000m, "Cash");
        await PayOkAsync(owner, ownerToken, sold.Id, 100_000m, "Cash");

        await CancelOkAsync(owner, ownerToken, sold.Id);

        var refunds = (await StoredPaymentsAsync(sold.Id)).Where(payment => payment.Kind == PaymentKind.Refund).ToList();
        refunds.Count.ShouldBe(2);
        refunds.Single(refund => refund.Method == PaymentMethod.Card).Amount.ShouldBe(600_000m);
        refunds.Single(refund => refund.Method == PaymentMethod.Cash).Amount.ShouldBe(300_000m);
    }

    [Fact]
    public async Task Cancel_PartlyPaid_RefundsOnlyWhatWasPaid()
    {
        var (owner, ownerToken) = await OwnerClientAsync();
        var sold = await SellAsync(owner, ownerToken);
        await PayOkAsync(owner, ownerToken, sold.Id, 250_000m, "Cash");

        await CancelOkAsync(owner, ownerToken, sold.Id);

        (await StoredPaymentsAsync(sold.Id)).Where(payment => payment.Kind == PaymentKind.Refund)
            .Sum(refund => refund.Amount).ShouldBe(250_000m);
    }

    /// <summary>A refused cancel writes no refund: the subscription and its money stay as they were.</summary>
    [Fact]
    public async Task Cancel_AfterASessionIsUsed_RefundsNothing()
    {
        var (owner, ownerToken) = await OwnerClientAsync();
        var sold = await SellAsync(owner, ownerToken);
        await PayOkAsync(owner, ownerToken, sold.Id, 900_000m, "Cash");
        using var checkIn = await TestLockers.CheckInAsync(owner, ownerToken, sold.MemberId);
        checkIn.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var response = await CancelAsync(owner, ownerToken, sold.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Subscriptions.AlreadyUsed");
        (await StoredPaymentsAsync(sold.Id)).ShouldAllBe(payment => payment.Kind == PaymentKind.Payment);
    }

    /// <summary>
    /// A cancelled subscription takes no more payments: its only refund was the cancel, so money paid
    /// onto it afterwards could never come back (§4 Cancel).
    /// </summary>
    [Fact]
    public async Task Pay_CancelledSubscription_Returns422Cancelled()
    {
        var (owner, ownerToken) = await OwnerClientAsync();
        var sold = await SellAsync(owner, ownerToken);
        await CancelOkAsync(owner, ownerToken, sold.Id);

        using var response = await PayAsync(owner, ownerToken, sold.Id, 100_000m, "Cash");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Subscriptions.Cancelled");
        (await StoredPaymentsAsync(sold.Id)).ShouldBeEmpty();
    }

    /// <summary>Two presses of «لغو» at once: one cancels and refunds, the other refunds nothing.</summary>
    [Fact]
    public async Task Cancel_InParallel_CancelsOnceAndRefundsOnce()
    {
        var (owner, ownerToken) = await OwnerClientAsync();
        var sold = await SellAsync(owner, ownerToken);
        await PayOkAsync(owner, ownerToken, sold.Id, 900_000m, "Cash");

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => CancelAsync(owner, ownerToken, sold.Id)));

        try
        {
            responses.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        (await StoredPaymentsAsync(sold.Id)).Where(payment => payment.Kind == PaymentKind.Refund)
            .Sum(refund => refund.Amount).ShouldBe(900_000m);
    }

    /// <summary>
    /// A payment and the cancel at the same moment: whichever order the member lock gives them, no
    /// money is left on the cancelled subscription. Either the payment lands first and is refunded
    /// with the rest, or it waits and is refused.
    /// </summary>
    [Fact]
    public async Task CancelAndPay_InParallel_LeaveNothingPaidOnTheCancelledSubscription()
    {
        var (owner, ownerToken) = await OwnerClientAsync();
        var sold = await SellAsync(owner, ownerToken);
        await PayOkAsync(owner, ownerToken, sold.Id, 300_000m, "Cash");

        var cancel = CancelAsync(owner, ownerToken, sold.Id);
        var pay = PayAsync(owner, ownerToken, sold.Id, 200_000m, "Card");
        using var cancelled = await cancel;
        using var paid = await pay;

        cancelled.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payments = await StoredPaymentsAsync(sold.Id);
        payments.Sum(payment => payment.Kind == PaymentKind.Payment ? payment.Amount : -payment.Amount).ShouldBe(0m);
    }

    /// <summary>No refund is written by hand any more (§5): the old route answers nothing.</summary>
    [Fact]
    public async Task RefundRoute_Removed_Returns404()
    {
        var (owner, ownerToken) = await OwnerClientAsync();
        var sold = await SellAsync(owner, ownerToken);
        await PayOkAsync(owner, ownerToken, sold.Id, 900_000m, "Cash");

        using var response = await SendAsync(
            owner, ownerToken, $"/api/subscriptions/{sold.Id}/refunds", new { amount = 900_000m, method = "Cash", reason = Reason });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await StoredPaymentsAsync(sold.Id)).ShouldAllBe(payment => payment.Kind == PaymentKind.Payment);
    }

    // ---- Helpers ----

    private async Task<(HttpClient Client, string Token)> OwnerClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("owner", TestUsers.Password));
    }

    /// <summary>A new member sold 10 sessions for 900,000 (the <see cref="TestPlans"/> default).</summary>
    private async Task<SubscriptionResponse> SellAsync(HttpClient client, string token)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        Member member = TestMembers.Seed("رضا احمدی", $"+98913{suffix:D7}");
        await using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Members.Add(member);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var plan = await TestPlans.AddAsync(Fixture);
        using var response = await SendAsync(client, token, $"/api/members/{member.Id}/subscriptions", plan.Body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await ReadAsync(response);
    }

    private static Task<HttpResponseMessage> PayAsync(HttpClient client, string token, Guid subscriptionId, decimal amount, string method) =>
        SendAsync(client, token, $"/api/subscriptions/{subscriptionId}/payments", new { amount, method });

    private static async Task PayOkAsync(HttpClient client, string token, Guid subscriptionId, decimal amount, string method)
    {
        using var response = await PayAsync(client, token, subscriptionId, amount, method);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static Task<HttpResponseMessage> CancelAsync(HttpClient client, string token, Guid id) =>
        SendAsync(client, token, $"/api/subscriptions/{id}/cancel", new { reason = Reason });

    private static async Task<SubscriptionResponse> CancelOkAsync(HttpClient client, string token, Guid id)
    {
        using var response = await CancelAsync(client, token, id);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await ReadAsync(response);
    }

    private async Task<List<Payment>> StoredPaymentsAsync(Guid subscriptionId)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Payments.AsNoTracking()
            .Where(payment => payment.SubscriptionId == subscriptionId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<SubscriptionResponse> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<SubscriptionResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string token, string path, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }
}
