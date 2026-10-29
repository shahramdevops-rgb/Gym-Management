using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Api.IntegrationTests.SiteAuth;
using Gym.Application.Cafe;
using Gym.Application.Subscriptions;
using Gym.Domain.Coaching;
using Gym.Domain.Members;
using Gym.Domain.Trainers;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.SiteRecords;

/// <summary>
/// The member's own records on the site (BUSINESS_RULES.md §15 <i>The member's own records</i>,
/// task 15.5a): everything the profile holds but the SMS, read-only, without the desk's notes and
/// the staff's names and reasons, and only the signed-in member's own.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SiteRecordsTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string MemberPhone = "+989121234567";
    private const string OtherMemberPhone = "+989131234567";
    private const string TrainerPhone = "+989351234567";

    private const string DetailsPath = "/api/site/me/details";
    private const string PlanPath = "/api/site/me/plan";
    private const string DebtPath = "/api/site/me/debt";
    private const string VisitsPath = "/api/site/me/visits";
    private const string PlansPath = "/api/site/me/plans";
    private const string PaymentsPath = "/api/site/me/payments";
    private const string CafeOrdersPath = "/api/site/me/cafe-orders";

    public static TheoryData<string> AllPaths =>
        [DetailsPath, PlanPath, DebtPath, VisitsPath, PlansPath, PaymentsPath, CafeOrdersPath];

    // ---- Details ----

    [Fact]
    public async Task Details_SignedInMember_ReturnsTheirDetailsWithoutTheDesksNote()
    {
        var member = await AddMemberAsync(MemberPhone, notes: "بدهی را تلفنی پیگیری کنید");
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);

        var body = await GetOkAsync(client, DetailsPath, token);

        body.GetProperty("fullName").GetString().ShouldBe(member.FullName);
        body.GetProperty("phoneNumber").GetString().ShouldBe(MemberPhone);
        body.GetProperty("birthDate").GetString().ShouldBe(member.BirthDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        body.GetProperty("joinedAt").GetDateTimeOffset().ShouldBe(member.CreatedAt, TimeSpan.FromMilliseconds(1));
        body.GetProperty("signedUpAt").GetDateTimeOffset().ShouldBe(await SignedUpAtAsync(member.Id), TimeSpan.FromMilliseconds(1));
        body.TryGetProperty("notes", out _).ShouldBeFalse();
        body.GetRawText().ShouldNotContain("پیگیری");
    }

    // ---- The plan now and the one bought for later ----

    [Fact]
    public async Task Plan_ActivePlanAndOneBoughtForLater_ReturnsBoth()
    {
        var member = await AddMemberAsync(MemberPhone);
        var (owner, ownerToken) = await OwnerClientAsync();
        var current = await SellAsync(owner, ownerToken, member.Id, 900_000m);
        var later = await SellAsync(owner, ownerToken, member.Id, 500_000m);
        await PayAsync(owner, ownerToken, current.Id, 300_000m);
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);

        var body = await GetOkAsync(client, PlanPath, token);

        var now = body.GetProperty("current");
        now.GetProperty("id").GetGuid().ShouldBe(current.Id);
        now.GetProperty("status").GetString().ShouldBe("Active");
        now.GetProperty("remainingSessions").GetInt32().ShouldBe(10);
        now.GetProperty("netPaid").GetDecimal().ShouldBe(300_000m);
        now.GetProperty("paymentStatus").GetString().ShouldBe("Partial");
        var next = body.GetProperty("next");
        next.GetProperty("id").GetGuid().ShouldBe(later.Id);
        next.GetProperty("status").GetString().ShouldBe("Upcoming");
    }

    [Fact]
    public async Task Plan_MemberWithNoPlan_ReturnsNeither()
    {
        await AddMemberAsync(MemberPhone);
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);

        var body = await GetOkAsync(client, PlanPath, token);

        body.GetProperty("current").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("next").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // ---- Debt ----

    [Fact]
    public async Task Debt_PartlyPaidPlanAndUnpaidCafePurchase_ListsEachItem()
    {
        var member = await AddMemberAsync(MemberPhone);
        var (owner, ownerToken) = await OwnerClientAsync();
        var plan = await SellAsync(owner, ownerToken, member.Id, 900_000m);
        await PayAsync(owner, ownerToken, plan.Id, 300_000m);
        var water = await AddProductAsync(owner, ownerToken, "آب معدنی", 15_000m);
        await BuyAsync(owner, ownerToken, member.Id, water, quantity: 2);
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);

        var body = await GetOkAsync(client, DebtPath, token);

        body.GetProperty("total").GetDecimal().ShouldBe(630_000m);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        items.Count.ShouldBe(2);
        var planItem = items.Single(item => item.GetProperty("kind").GetString() == "Subscription");
        planItem.GetProperty("outstanding").GetDecimal().ShouldBe(600_000m);
        var cafeItem = items.Single(item => item.GetProperty("kind").GetString() == "CafeOrder");
        cafeItem.GetProperty("outstanding").GetDecimal().ShouldBe(30_000m);
        var line = cafeItem.GetProperty("cafeItems").EnumerateArray().ShouldHaveSingleItem();
        line.GetProperty("productName").GetString().ShouldBe("آب معدنی");
        line.GetProperty("quantity").GetInt32().ShouldBe(2);
        line.TryGetProperty("productId", out _).ShouldBeFalse();
    }

    // ---- History ----

    [Fact]
    public async Task Visits_CheckInWithCardioAndOut_ListsTheVisitWithItsLockerWithoutWhoRecordedTheCharge()
    {
        var member = await AddMemberAsync(MemberPhone);
        var (owner, ownerToken) = await OwnerClientAsync();
        await SellAsync(owner, ownerToken, member.Id, 900_000m);
        var visit = await TestLockers.CheckInOkAsync(owner, ownerToken, member.Id, lockerNumber: 5);
        await PostOkAsync(owner, ownerToken, $"/api/attendance/{visit.Id}/service-charges", new { kind = "Cardio", amount = 50_000m });
        await PostOkAsync(owner, ownerToken, $"/api/attendance/{visit.Id}/check-out", body: null);
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);

        var page = await GetOkAsync(client, VisitsPath, token);

        page.GetProperty("totalCount").GetInt32().ShouldBe(1);
        var row = page.GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        row.GetProperty("id").GetGuid().ShouldBe(visit.Id);
        row.GetProperty("lockerNumber").GetInt32().ShouldBe(5);
        row.GetProperty("checkedOutAt").ValueKind.ShouldNotBe(JsonValueKind.Null);
        var charge = row.GetProperty("charges").EnumerateArray().ShouldHaveSingleItem();
        charge.GetProperty("kind").GetString().ShouldBe("Cardio");
        charge.GetProperty("amount").GetDecimal().ShouldBe(50_000m);
        charge.TryGetProperty("recordedByUserId", out _).ShouldBeFalse();
        row.TryGetProperty("guestName", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Plans_CancelledPlan_IsMarkedWithoutTheReason()
    {
        var member = await AddMemberAsync(MemberPhone);
        var (owner, ownerToken) = await OwnerClientAsync();
        var kept = await SellAsync(owner, ownerToken, member.Id, 900_000m);
        var cancelled = await SellAsync(owner, ownerToken, member.Id, 500_000m);
        await PostOkAsync(owner, ownerToken, $"/api/subscriptions/{cancelled.Id}/cancel", new { reason = "اشتباه صندوق" });
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);

        var page = await GetOkAsync(client, PlansPath, token);

        var rows = page.GetProperty("items").EnumerateArray().ToList();
        rows.Select(row => row.GetProperty("id").GetGuid()).ShouldBe([cancelled.Id, kept.Id]);
        rows[0].GetProperty("status").GetString().ShouldBe("Cancelled");
        rows[0].GetProperty("cancelledAt").ValueKind.ShouldNotBe(JsonValueKind.Null);
        rows[0].TryGetProperty("cancellationReason", out _).ShouldBeFalse();
        page.GetRawText().ShouldNotContain("اشتباه صندوق");
    }

    [Fact]
    public async Task Payments_PaymentAndItsRefund_AreListedWithoutWhoTookThemOrTheReason()
    {
        var member = await AddMemberAsync(MemberPhone);
        var (owner, ownerToken) = await OwnerClientAsync();
        await SellAsync(owner, ownerToken, member.Id, 900_000m);
        var later = await SellAsync(owner, ownerToken, member.Id, 500_000m);
        await PayAsync(owner, ownerToken, later.Id, 200_000m);
        // Cancelling the queued plan refunds what was paid on it (BUSINESS_RULES.md §4 Cancel).
        await PostOkAsync(owner, ownerToken, $"/api/subscriptions/{later.Id}/cancel", new { reason = "انصراف عضو" });
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);

        var page = await GetOkAsync(client, PaymentsPath, token);

        var rows = page.GetProperty("items").EnumerateArray().ToList();
        rows.Select(row => row.GetProperty("kind").GetString()).Order().ShouldBe(["Payment", "Refund"]);
        rows.ShouldAllBe(row => row.GetProperty("targetId").GetGuid() == later.Id && row.GetProperty("amount").GetDecimal() == 200_000m);
        foreach (var row in rows)
        {
            row.TryGetProperty("receivedByUserId", out _).ShouldBeFalse();
            row.TryGetProperty("reason", out _).ShouldBeFalse();
        }
        page.GetRawText().ShouldNotContain("انصراف عضو");
    }

    [Fact]
    public async Task CafeOrders_CancelledPurchase_IsMarkedWithoutTheReasonOrTheSeller()
    {
        var member = await AddMemberAsync(MemberPhone);
        var (owner, ownerToken) = await OwnerClientAsync();
        var water = await AddProductAsync(owner, ownerToken, "آب معدنی", 15_000m);
        var order = await BuyAsync(owner, ownerToken, member.Id, water, quantity: 1);
        await PostOkAsync(owner, ownerToken, $"/api/cafe/orders/{order.Id}/cancel", new { reason = "اشتباه ثبت شد" });
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);

        var page = await GetOkAsync(client, CafeOrdersPath, token);

        var row = page.GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        row.GetProperty("id").GetGuid().ShouldBe(order.Id);
        row.GetProperty("cancelledAt").ValueKind.ShouldNotBe(JsonValueKind.Null);
        row.GetProperty("outstanding").GetDecimal().ShouldBe(0m);
        row.GetProperty("items").EnumerateArray().ShouldHaveSingleItem().GetProperty("productName").GetString().ShouldBe("آب معدنی");
        row.TryGetProperty("placedByUserId", out _).ShouldBeFalse();
        row.TryGetProperty("cancelReason", out _).ShouldBeFalse();
        page.GetRawText().ShouldNotContain("اشتباه ثبت شد");
    }

    // ---- Only the member's own ----

    [Fact]
    public async Task Records_AnotherMembersRecords_AreNotIncluded()
    {
        await AddMemberAsync(MemberPhone);
        var other = await AddMemberAsync(OtherMemberPhone);
        var (owner, ownerToken) = await OwnerClientAsync();
        var plan = await SellAsync(owner, ownerToken, other.Id, 900_000m);
        await PayAsync(owner, ownerToken, plan.Id, 300_000m);
        await TestLockers.CheckInOkAsync(owner, ownerToken, other.Id);
        var water = await AddProductAsync(owner, ownerToken, "آب معدنی", 15_000m);
        await BuyAsync(owner, ownerToken, other.Id, water, quantity: 1);
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);

        (await GetOkAsync(client, DetailsPath, token)).GetProperty("phoneNumber").GetString().ShouldBe(MemberPhone);
        (await GetOkAsync(client, PlanPath, token)).GetProperty("current").ValueKind.ShouldBe(JsonValueKind.Null);
        (await GetOkAsync(client, DebtPath, token)).GetProperty("total").GetDecimal().ShouldBe(0m);
        foreach (var path in new[] { VisitsPath, PlansPath, PaymentsPath, CafeOrdersPath })
        {
            (await GetOkAsync(client, path, token)).GetProperty("totalCount").GetInt32().ShouldBe(0, path);
        }
    }

    // ---- Who may ask ----

    [Theory]
    [MemberData(nameof(AllPaths))]
    public async Task Records_StaffToken_Returns401(string path)
    {
        var (owner, ownerToken) = await OwnerClientAsync();

        using var response = await owner.SendAsync(Get(path, ownerToken), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(AllPaths))]
    public async Task Records_TrainerWhoIsNotAMember_Returns403(string path)
    {
        await AddTrainerAsync(TrainerPhone);
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, TrainerPhone);

        using var response = await client.SendAsync(Get(path, token), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(AllPaths))]
    public async Task Records_MemberDeactivatedAfterSigningIn_Returns403(string path)
    {
        // The token still says member for up to 15 minutes; the handler reads the desk's now.
        var member = await AddMemberAsync(MemberPhone);
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);
        await DeactivateAsync(member.Id);

        using var response = await client.SendAsync(Get(path, token), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.MemberInactive.Code);
    }

    // ---- Helpers ----

    private static HttpRequestMessage Get(string path, string token) => new HttpRequestMessage(HttpMethod.Get, path).WithBearer(token);

    private static async Task<JsonElement> GetOkAsync(HttpClient client, string path, string token)
    {
        using var response = await client.SendAsync(Get(path, token), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, path);

        return await response.ReadJsonAsync();
    }

    private static async Task<JsonElement> PostOkAsync(HttpClient client, string token, string path, object? body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
        response.IsSuccessStatusCode.ShouldBeTrue($"{path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");

        return await response.ReadJsonAsync();
    }

    private async Task<string> SignInAsync(HttpClient client, string phone)
    {
        using var signIn = await client.SignInWithCodeAsync(Fixture, phone);
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await signIn.ReadJsonAsync()).GetProperty("access").GetProperty("accessToken").GetString()!;
    }

    private async Task<(HttpClient Client, string Token)> OwnerClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("owner", TestUsers.Password));
    }

    /// <summary>Sold through the panel's endpoint; a second sale is queued behind the first.</summary>
    private async Task<SubscriptionResponse> SellAsync(HttpClient client, string token, Guid memberId, decimal price)
    {
        var plan = await TestPlans.AddAsync(Fixture, sessions: 10, price: price);

        return (await PostOkAsync(client, token, $"/api/members/{memberId}/subscriptions", plan.Body))
            .Deserialize<SubscriptionResponse>(JsonSerializerOptions.Web).ShouldNotBeNull();
    }

    private static async Task PayAsync(HttpClient client, string token, Guid subscriptionId, decimal amount) =>
        await PostOkAsync(client, token, $"/api/subscriptions/{subscriptionId}/payments", new { amount, method = "Cash" });

    private static async Task<Guid> AddProductAsync(HttpClient client, string token, string name, decimal price)
    {
        var category = await PostOkAsync(client, token, "/api/cafe/categories", new { name = $"دسته {name}" });

        var product = await PostOkAsync(client, token, "/api/cafe/products", new
        {
            name,
            categoryId = category.GetProperty("id").GetGuid(),
            price = price.ToString(CultureInfo.InvariantCulture),
        });

        return product.GetProperty("id").GetGuid();
    }

    /// <summary>A purchase on the member's account, left unpaid.</summary>
    private static async Task<CafeOrderResponse> BuyAsync(HttpClient client, string token, Guid memberId, Guid productId, int quantity) =>
        (await PostOkAsync(client, token, "/api/cafe/orders", new
        {
            memberId,
            items = new[] { new { productId, quantity } },
            payment = (object?)null,
        }))
        .Deserialize<CafeOrderResponse>(JsonSerializerOptions.Web).ShouldNotBeNull();

    private async Task<Member> AddMemberAsync(string phone, string? notes = null)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var member = notes is null
            ? TestMembers.Seed("علی کریمی", phone)
            : Member.Create("علی کریمی", phone, notes, new DateOnly(1990, 4, 1), today: new DateOnly(2026, 1, 1)).Value;
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    private async Task AddTrainerAsync(string phone)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Trainers.Add(Trainer.Create("رضا محمدی", phone, new DateOnly(2026, 10, 1)).Value);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task DeactivateAsync(Guid memberId)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var member = await db.Members.SingleAsync(row => row.Id == memberId, TestContext.Current.CancellationToken);
        member.Deactivate();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<DateTimeOffset> SignedUpAtAsync(Guid memberId)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().SiteAccounts.AsNoTracking()
            .Where(account => account.MemberId == memberId)
            .Select(account => account.CreatedAt)
            .SingleAsync(TestContext.Current.CancellationToken);
    }
}
