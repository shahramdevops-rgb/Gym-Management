using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Attendances;
using Gym.Application.Cafe;
using Gym.Application.Members.GetMemberDebt;
using Gym.Application.Payments.SettleMemberDebt;
using Gym.Domain.Members;
using Gym.Domain.Payments;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Payments;

/// <summary>
/// <c>POST /api/members/{id}/settlements</c>: one amount over several owed items
/// (BUSINESS_RULES.md §5 <i>Settling several items at once</i>, task 7.5). The member in every test
/// is the walk-in of the rule: a subscription, a هوازی charge and a cafe purchase, all unpaid.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SettlementEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const decimal PlanPrice = 150_000m;
    private const decimal CardioAmount = 40_000m;
    private const decimal DrinkPrice = 30_000m;
    private const decimal Everything = PlanPrice + CardioAmount + DrinkPrice;

    private static int _phoneSuffix;

    [Fact]
    public async Task Settle_TheWholeDebt_PaysEveryItemAndClearsTheDebt()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await VisitWithThreeItemsAsync(client, token);
        var debt = await GetDebtOkAsync(client, token, visit.MemberId!.Value);

        using var response = await SettleAsync(client, token, visit.MemberId!.Value, Everything, debt.Items, method: "Card");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var settlement = await ReadAsync(response);
        settlement.Amount.ShouldBe(Everything);
        settlement.Method.ShouldBe(PaymentMethod.Card);
        settlement.RemainingDebt.ShouldBe(0m);
        settlement.Payments.Select(payment => (payment.Kind, payment.Amount, payment.Outstanding)).ShouldBe(
        [
            (PaymentTargetKind.CafeOrder, DrinkPrice, 0m),
            (PaymentTargetKind.ServiceCharge, CardioAmount, 0m),
            (PaymentTargetKind.Subscription, PlanPrice, 0m),
        ]);

        (await GetDebtOkAsync(client, token, visit.MemberId!.Value)).Total.ShouldBe(0m);
    }

    [Fact]
    public async Task Settle_TheWholeDebt_WritesOneOrdinaryPaymentPerItemAtOneMoment()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await VisitWithThreeItemsAsync(client, token);
        var debt = await GetDebtOkAsync(client, token, visit.MemberId!.Value);

        using var response = await SettleAsync(client, token, visit.MemberId!.Value, Everything, debt.Items, reference: "ref-7");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var stored = await StoredPaymentsAsync((await ReadAsync(response)).Payments.Select(payment => payment.PaymentId));
        stored.Count.ShouldBe(3);
        stored.ShouldAllBe(payment => payment.Kind == PaymentKind.Payment && payment.Method == PaymentMethod.Cash);
        stored.ShouldAllBe(payment => payment.ReferenceNumber == "ref-7");
        stored.Select(payment => payment.PaidAt).Distinct().ShouldHaveSingleItem();
        stored.Count(payment => payment.SubscriptionId != null).ShouldBe(1);
        stored.Count(payment => payment.ServiceChargeId != null).ShouldBe(1);
        stored.Count(payment => payment.CafeOrderId != null).ShouldBe(1);
    }

    [Fact]
    public async Task Settle_LessThanTheTotal_PaysCafeThenCardioThenPartOfTheSubscription()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await VisitWithThreeItemsAsync(client, token);
        var debt = await GetDebtOkAsync(client, token, visit.MemberId!.Value);

        using var response = await SettleAsync(client, token, visit.MemberId!.Value, 100_000m, debt.Items);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var settlement = await ReadAsync(response);
        settlement.Payments.Select(payment => (payment.Kind, payment.Amount)).ShouldBe(
        [
            (PaymentTargetKind.CafeOrder, DrinkPrice),
            (PaymentTargetKind.ServiceCharge, CardioAmount),
            (PaymentTargetKind.Subscription, 30_000m),
        ]);
        settlement.RemainingDebt.ShouldBe(Everything - 100_000m);

        var after = await GetDebtOkAsync(client, token, visit.MemberId!.Value);
        after.Items.ShouldHaveSingleItem().Kind.ShouldBe(PaymentTargetKind.Subscription);
        after.Items.Single().Outstanding.ShouldBe(PlanPrice - 30_000m);
    }

    [Fact]
    public async Task Settle_SomeItemsUnticked_LeavesThemOwedInFull()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await VisitWithThreeItemsAsync(client, token);
        var debt = await GetDebtOkAsync(client, token, visit.MemberId!.Value);
        var ticked = debt.Items.Where(item => item.Kind != PaymentTargetKind.Subscription).ToList();

        using var response = await SettleAsync(client, token, visit.MemberId!.Value, CardioAmount + DrinkPrice, ticked);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync(response)).RemainingDebt.ShouldBe(PlanPrice);
        var after = await GetDebtOkAsync(client, token, visit.MemberId!.Value);
        after.Items.ShouldHaveSingleItem().Outstanding.ShouldBe(PlanPrice);
    }

    [Fact]
    public async Task Settle_MoreThanTheTickedTotal_Returns422OverpaymentAndWritesNothing()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await VisitWithThreeItemsAsync(client, token);
        var debt = await GetDebtOkAsync(client, token, visit.MemberId!.Value);
        var drinkOnly = debt.Items.Where(item => item.Kind == PaymentTargetKind.CafeOrder).ToList();

        using var response = await SettleAsync(client, token, visit.MemberId!.Value, DrinkPrice + 1m, drinkOnly);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Payments.Overpayment");
        (await GetDebtOkAsync(client, token, visit.MemberId!.Value)).Total.ShouldBe(Everything);
    }

    [Fact]
    public async Task Settle_ItemPaidSinceItWasShown_Returns409DebtChangedAndWritesNothing()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await VisitWithThreeItemsAsync(client, token);
        var shown = await GetDebtOkAsync(client, token, visit.MemberId!.Value);
        var drink = shown.Items.Single(item => item.Kind == PaymentTargetKind.CafeOrder);
        using (var paid = await SendAsync(client, token, HttpMethod.Post, $"/api/cafe/orders/{drink.Id}/payments",
            new { amount = 10_000m, method = "Cash" }))
        {
            paid.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        using var response = await SettleAsync(client, token, visit.MemberId!.Value, Everything, shown.Items);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Settlements.DebtChanged");
        (await GetDebtOkAsync(client, token, visit.MemberId!.Value)).Total.ShouldBe(Everything - 10_000m);
    }

    [Fact]
    public async Task Settle_ChargeVoidedSinceItWasShown_Returns409DebtChanged()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await VisitWithThreeItemsAsync(client, token);
        var shown = await GetDebtOkAsync(client, token, visit.MemberId!.Value);
        var cardio = shown.Items.Single(item => item.Kind == PaymentTargetKind.ServiceCharge);
        using (var voided = await SendAsync(client, token, HttpMethod.Post, $"/api/service-charges/{cardio.Id}/void",
            new { reason = "اشتباه" }))
        {
            voided.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var response = await SettleAsync(client, token, visit.MemberId!.Value, Everything, shown.Items);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Settlements.DebtChanged");
    }

    [Fact]
    public async Task Settle_AnotherMembersItem_Returns409DebtChanged()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await VisitWithThreeItemsAsync(client, token);
        var other = await VisitWithThreeItemsAsync(client, token);
        var othersDebt = await GetDebtOkAsync(client, token, other.MemberId!.Value);

        using var response = await SettleAsync(client, token, visit.MemberId!.Value, Everything, othersDebt.Items);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Settlements.DebtChanged");
        (await GetDebtOkAsync(client, token, other.MemberId!.Value)).Total.ShouldBe(Everything);
    }

    [Fact]
    public async Task Settle_NoItems_Returns400WithFieldCode()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await VisitWithThreeItemsAsync(client, token);

        using var response = await SettleAsync(client, token, visit.MemberId!.Value, 10_000m, []);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("errors").GetProperty("items")[0].GetProperty("code").GetString()
            .ShouldBe("Settlements.NoItems");
    }

    [Fact]
    public async Task Settle_UnknownMember_Returns404()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await VisitWithThreeItemsAsync(client, token);
        var debt = await GetDebtOkAsync(client, token, visit.MemberId!.Value);

        using var response = await SettleAsync(client, token, Guid.CreateVersion7(), Everything, debt.Items);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Members.NotFound");
    }

    /// <summary>
    /// Two desks pressing «تسویه» for the same member at once: the second waits for the member
    /// lock, finds every figure changed, and writes nothing. Never paid twice.
    /// </summary>
    [Fact]
    public async Task Settle_FourTimesInParallel_OnlyOneGoesThrough()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await VisitWithThreeItemsAsync(client, token);
        var debt = await GetDebtOkAsync(client, token, visit.MemberId!.Value);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => SettleAsync(client, token, visit.MemberId!.Value, Everything, debt.Items)));

        try
        {
            responses.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBe(1);
            responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).ShouldBe(3);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        (await StoredPaymentsForMemberAsync(visit.MemberId!.Value)).Sum(payment => payment.Amount).ShouldBe(Everything);
    }

    // ---- Helpers ----

    private async Task<(HttpClient Client, string Token)> StaffClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    /// <summary>
    /// A member inside the gym owing three things: the plan, هوازی, and a drink bought on the
    /// board. Nothing paid.
    /// </summary>
    private async Task<AttendanceResponse> VisitWithThreeItemsAsync(HttpClient client, string token)
    {
        var member = await AddMemberAsync();
        var plan = await TestPlans.AddAsync(Fixture, price: PlanPrice);

        await PostOkAsync<object>(client, token, $"/api/members/{member.Id}/subscriptions", plan.Body);

        using var checkedIn = await TestLockers.CheckInAsync(client, token, member.Id);
        checkedIn.StatusCode.ShouldBe(HttpStatusCode.Created);
        var visit = (await checkedIn.Content.ReadFromJsonAsync<AttendanceResponse>(
            TestContext.Current.CancellationToken)).ShouldNotBeNull();

        await PostOkAsync<object>(client, token, $"/api/attendance/{visit.Id}/service-charges",
            new { kind = "Cardio", amount = CardioAmount });

        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var category = await PostOkAsync<ProductCategoryResponse>(
            client, token, "/api/cafe/categories", new { name = $"نوشیدنی {suffix}" });
        var drink = await PostOkAsync<ProductResponse>(client, token, "/api/cafe/products", new
        {
            name = $"آب معدنی {suffix}",
            categoryId = category.Id,
            price = DrinkPrice.ToString(CultureInfo.InvariantCulture),
        });
        await PostOkAsync<object>(client, token, "/api/cafe/orders", new
        {
            memberId = member.Id,
            attendanceId = visit.Id,
            items = new[] { new { productId = drink.Id, quantity = 1 } },
            payment = (object?)null,
        });

        return visit;
    }

    private async Task<Member> AddMemberAsync()
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed($"مراجع {suffix}", $"+98916{suffix:D7}");

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    private async Task<List<Payment>> StoredPaymentsAsync(IEnumerable<Guid> ids)
    {
        var wanted = ids.ToList();
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Payments
            .AsNoTracking()
            .Where(payment => wanted.Contains(payment.Id))
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Every payment against anything of this member's: subscriptions, charges and orders.</summary>
    private async Task<List<Payment>> StoredPaymentsForMemberAsync(Guid memberId)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Payments
            .AsNoTracking()
            .Where(payment =>
                db.Subscriptions.Any(s => s.Id == payment.SubscriptionId && s.MemberId == memberId) ||
                db.ServiceCharges.Any(c => c.Id == payment.ServiceChargeId && c.MemberId == memberId) ||
                db.CafeOrders.Any(o => o.Id == payment.CafeOrderId && o.MemberId == memberId))
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> SettleAsync(
        HttpClient client,
        string token,
        Guid memberId,
        decimal amount,
        IEnumerable<MemberDebtItemResponse> items,
        string method = "Cash",
        string? reference = null) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/members/{memberId}/settlements", new
        {
            amount,
            method,
            referenceNumber = reference,
            items = items.Select(item => new { kind = item.Kind.ToString(), id = item.Id, outstanding = item.Outstanding }),
        });

    private static async Task<MemberDebtResponse> GetDebtOkAsync(HttpClient client, string token, Guid memberId)
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, $"/api/members/{memberId}/debt");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<MemberDebtResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<SettlementResponse> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<SettlementResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

    private static async Task<T> PostOkAsync<T>(HttpClient client, string token, string path, object body)
        where T : class
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, path, body);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client, string token, HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }
}
