using System.Globalization;
using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Cafe;
using Gym.Application.Members.GetMemberDebt;
using Gym.Application.Payments;
using Gym.Domain.Payments;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Cafe;

/// <summary>
/// <c>/api/cafe/orders</c> (BUSINESS_RULES.md §8, task 7.2). Nothing here counts stock: the point
/// of these tests is the money — the price snapshot, the walk-in rule, and an order on a member's
/// account turning into their debt.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class CafeOrderEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string OrdersPath = "/api/cafe/orders";
    private const string ProductsPath = "/api/cafe/products";
    private const string CategoriesPath = "/api/cafe/categories";

    private static int _phoneSuffix;

    // ---- Creating an order ----

    [Fact]
    public async Task CreateOrder_WalkInPaidInFull_Returns201WithTheTotalAndPaidStatus()
    {
        var (client, token) = await StaffClientAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);

        var order = await CreateOkAsync(client, token, memberId: null, [(water.Id, 2)], paid: 30_000m);

        order.TotalAmount.ShouldBe(30_000m);
        order.MemberId.ShouldBeNull();
        order.NetPaid.ShouldBe(30_000m);
        order.PaymentStatus.ShouldBe(PaymentStatus.Paid);
        order.Outstanding.ShouldBe(0m);
        order.Items.ShouldHaveSingleItem().Quantity.ShouldBe(2);
    }

    [Fact]
    public async Task CreateOrder_WalkInWithNoPayment_Returns422()
    {
        // There is no account to leave a balance on (BUSINESS_RULES.md §8).
        var (client, token) = await StaffClientAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);

        using var response = await CreateAsync(client, token, memberId: null, [(water.Id, 1)], paid: null);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("CafeOrders.WalkInMustBePaidInFull");
        (await CountOrdersAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task CreateOrder_WalkInPaidInPart_Returns422()
    {
        var (client, token) = await StaffClientAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);

        using var response = await CreateAsync(client, token, memberId: null, [(water.Id, 2)], paid: 10_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("CafeOrders.WalkInMustBePaidInFull");
    }

    [Fact]
    public async Task CreateOrder_OnAMembersAccountUnpaid_Returns201Unpaid()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);

        var order = await CreateOkAsync(client, token, member.Id, [(water.Id, 2)], paid: null);

        order.MemberId.ShouldBe(member.Id);
        order.MemberFullName.ShouldBe(member.FullName);
        order.NetPaid.ShouldBe(0m);
        order.PaymentStatus.ShouldBe(PaymentStatus.Unpaid);
        order.Outstanding.ShouldBe(30_000m);
    }

    [Fact]
    public async Task CreateOrder_OnAMembersAccountPartlyPaid_Returns201Partial()
    {
        // Whatever the customer hands over is registered; the rest stays on the account (§5).
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);

        var order = await CreateOkAsync(client, token, member.Id, [(water.Id, 2)], paid: 10_000m);

        order.NetPaid.ShouldBe(10_000m);
        order.PaymentStatus.ShouldBe(PaymentStatus.Partial);
        order.Outstanding.ShouldBe(20_000m);
    }

    [Fact]
    public async Task CreateOrder_PaidMoreThanTheTotal_Returns422()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);

        using var response = await CreateAsync(client, token, member.Id, [(water.Id, 1)], paid: 20_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("CafeOrders.PaidMoreThanTheOrder");
        (await CountOrdersAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task CreateOrder_SeveralLines_TotalsThemAll()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var cake = await AddProductAsync(client, token, "کیک", 25_000m);

        var order = await CreateOkAsync(client, token, member.Id, [(water.Id, 2), (cake.Id, 1)], paid: null);

        order.TotalAmount.ShouldBe(55_000m);
        order.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task CreateOrder_UnknownMember_Returns404()
    {
        var (client, token) = await StaffClientAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);

        using var response = await CreateAsync(
            client, token, Guid.CreateVersion7(), [(water.Id, 1)], paid: null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Members.NotFound");
    }

    [Fact]
    public async Task CreateOrder_UnknownProduct_Returns404()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();

        using var response = await CreateAsync(
            client, token, member.Id, [(Guid.CreateVersion7(), 1)], paid: null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("CafeOrders.ProductNotFound");
    }

    [Fact]
    public async Task CreateOrder_InactiveProduct_Returns422AndWritesNothing()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        await PostOkAsync(client, token, $"{ProductsPath}/{water.Id}/deactivate");

        using var response = await CreateAsync(client, token, member.Id, [(water.Id, 1)], paid: null);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Products.Inactive");
        (await CountOrdersAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task CreateOrder_ProductInASwitchedOffCategory_Returns422()
    {
        // ناموجود is ناموجود, whichever of the two switches said so (BUSINESS_RULES.md §8).
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var category = await AddCategoryAsync(client, token, "نوشیدنی");
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m, category.Id);
        await PostOkAsync(client, token, $"{CategoriesPath}/{category.Id}/deactivate");

        using var response = await CreateAsync(client, token, member.Id, [(water.Id, 1)], paid: null);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Products.Inactive");
    }

    [Fact]
    public async Task CreateOrder_NoItems_Returns400()
    {
        var (client, token) = await StaffClientAsync();

        using var response = await CreateAsync(client, token, memberId: null, [], paid: null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateOrder_ZeroQuantity_Returns400()
    {
        var (client, token) = await StaffClientAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);

        using var response = await CreateAsync(client, token, memberId: null, [(water.Id, 0)], paid: null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateOrder_SameProductTwice_Returns400()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);

        using var response = await CreateAsync(
            client, token, member.Id, [(water.Id, 1), (water.Id, 2)], paid: null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).ShouldBe("CafeOrders.DuplicateProduct");
    }

    [Fact]
    public async Task CreateOrder_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.PostAsJsonAsync(
            OrdersPath,
            new { memberId = (Guid?)null, items = Array.Empty<object>(), payment = (object?)null },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- The snapshot ----

    [Fact]
    public async Task CreateOrder_ThenEditTheProduct_LeavesTheOrdersNameAndPriceAlone()
    {
        // What makes an editable price safe (BUSINESS_RULES.md §8).
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var order = await CreateOkAsync(client, token, member.Id, [(water.Id, 2)], paid: null);

        using (var edited = await SendAsync(
                   client,
                   token,
                   HttpMethod.Put,
                   $"{ProductsPath}/{water.Id}",
                   new
                   {
                       name = "آب معدنی بزرگ",
                       categoryId = water.CategoryId,
                       price = "20000",
                       version = water.Version,
                   }))
        {
            edited.EnsureSuccessStatusCode();
        }

        var reread = await GetOrderAsync(client, token, order.Id);

        var line = reread.Items.ShouldHaveSingleItem();
        line.ProductName.ShouldBe("آب معدنی");
        line.UnitPrice.ShouldBe(15_000m);
        reread.TotalAmount.ShouldBe(30_000m);
    }

    // ---- Debt ----

    [Fact]
    public async Task MemberDebt_UnpaidOrderOnAccount_AppearsInTheBreakdown()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var order = await CreateOkAsync(client, token, member.Id, [(water.Id, 2)], paid: null);

        var debt = await DebtAsync(client, token, member.Id);

        debt.Total.ShouldBe(30_000m);
        var item = debt.Items.ShouldHaveSingleItem();
        item.Kind.ShouldBe(PaymentTargetKind.CafeOrder);
        item.Id.ShouldBe(order.Id);
        item.Price.ShouldBe(30_000m);
        item.Outstanding.ShouldBe(30_000m);
    }

    [Fact]
    public async Task MemberDebt_WalkInOrder_IsNobodysDebt()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        await CreateOkAsync(client, token, memberId: null, [(water.Id, 1)], paid: 15_000m);

        (await DebtAsync(client, token, member.Id)).Total.ShouldBe(0m);
    }

    [Fact]
    public async Task MemberDebt_FullyPaidOrder_LeavesTheBreakdown()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        await CreateOkAsync(client, token, member.Id, [(water.Id, 1)], paid: 15_000m);

        var debt = await DebtAsync(client, token, member.Id);

        debt.Total.ShouldBe(0m);
        debt.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task MemberList_MemberWithAnUnpaidOrder_ShowsTheDebtOnTheRow()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        await CreateOkAsync(client, token, member.Id, [(water.Id, 3)], paid: null);

        using var response = await SendAsync(
            client, token, HttpMethod.Get, $"/api/members?Search={Uri.EscapeDataString(member.FullName)}", body: null);

        response.EnsureSuccessStatusCode();
        var page = (await response.Content.ReadFromJsonAsync<Application.Common.Paging.PagedResponse<Application.Members.MemberResponse>>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        page.Items.ShouldHaveSingleItem().Debt.ShouldBe(45_000m);
    }

    // ---- Settling later ----

    [Fact]
    public async Task PayOrder_InInstalments_AddsUpAndEndsPaid()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var order = await CreateOkAsync(client, token, member.Id, [(water.Id, 2)], paid: null);

        var first = await PayOkAsync(client, token, order.Id, 10_000m);
        first.TargetNetPaid.ShouldBe(10_000m);
        first.TargetPaymentStatus.ShouldBe(PaymentStatus.Partial);

        var second = await PayOkAsync(client, token, order.Id, 20_000m);
        second.TargetNetPaid.ShouldBe(30_000m);
        second.TargetPaymentStatus.ShouldBe(PaymentStatus.Paid);

        (await DebtAsync(client, token, member.Id)).Total.ShouldBe(0m);
    }

    [Fact]
    public async Task PayOrder_MoreThanIsOwed_Returns422AndTakesNothing()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var order = await CreateOkAsync(client, token, member.Id, [(water.Id, 1)], paid: null);

        using var response = await PayAsync(client, token, order.Id, 20_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Payments.Overpayment");
        (await GetOrderAsync(client, token, order.Id)).NetPaid.ShouldBe(0m);
    }

    [Fact]
    public async Task PayOrder_UnknownOrder_Returns404()
    {
        var (client, token) = await StaffClientAsync();

        using var response = await PayAsync(client, token, Guid.CreateVersion7(), 1_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("CafeOrders.NotFound");
    }

    [Fact]
    public async Task PayOrder_InParallel_NeverExceedsTheTotal()
    {
        // The same sum-across-rows invariant the other two payment handlers guard, and the same
        // member lock guarding it.
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var order = await CreateOkAsync(client, token, member.Id, [(water.Id, 2)], paid: null);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => PayAsync(client, token, order.Id, 20_000m)));

        try
        {
            responses.Count(response => response.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        (await GetOrderAsync(client, token, order.Id)).NetPaid.ShouldBe(20_000m);
    }

    // ---- Reading one back ----

    [Fact]
    public async Task GetOrder_UnknownId_Returns404()
    {
        var (client, token) = await StaffClientAsync();

        using var response = await SendAsync(
            client, token, HttpMethod.Get, $"{OrdersPath}/{Guid.CreateVersion7()}", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("CafeOrders.NotFound");
    }

    [Fact]
    public async Task GetOrder_WalkIn_HasNoMemberName()
    {
        var (client, token) = await StaffClientAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var order = await CreateOkAsync(client, token, memberId: null, [(water.Id, 1)], paid: 15_000m);

        var reread = await GetOrderAsync(client, token, order.Id);

        reread.MemberId.ShouldBeNull();
        reread.MemberFullName.ShouldBeNull();
    }

    // ---- Helpers ----

    private async Task<(HttpClient Client, string Token)> StaffClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    private async Task<Domain.Members.Member> AddMemberAsync()
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = Domain.Members.Member.Create(
            $"عضو {suffix}", $"+98912100{suffix:D4}", notes: null, birthDate: null, new DateOnly(2026, 9, 26)).Value;

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    private static async Task<ProductCategoryResponse> AddCategoryAsync(
        HttpClient client, string token, string name)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, CategoriesPath, new { name });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ProductCategoryResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<ProductResponse> AddProductAsync(
        HttpClient client, string token, string name, decimal price, Guid? categoryId = null)
    {
        var category = categoryId ?? (await AddCategoryAsync(client, token, $"دسته {name}")).Id;

        using var response = await SendAsync(
            client,
            token,
            HttpMethod.Post,
            ProductsPath,
            new { name, categoryId = category, price = Money(price) });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ProductResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> CreateAsync(
        HttpClient client,
        string token,
        Guid? memberId,
        IReadOnlyList<(Guid ProductId, int Quantity)> items,
        decimal? paid) =>
            SendAsync(
                client,
                token,
                HttpMethod.Post,
                OrdersPath,
                new
                {
                    memberId,
                    items = items.Select(item => new { productId = item.ProductId, quantity = item.Quantity }),
                    payment = paid is null
                        ? null
                        : new { amount = Money(paid.Value), method = "Cash", referenceNumber = (string?)null },
                });

    private static async Task<CafeOrderResponse> CreateOkAsync(
        HttpClient client,
        string token,
        Guid? memberId,
        IReadOnlyList<(Guid ProductId, int Quantity)> items,
        decimal? paid)
    {
        using var response = await CreateAsync(client, token, memberId, items, paid);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CafeOrderResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<CafeOrderResponse> GetOrderAsync(HttpClient client, string token, Guid id)
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, $"{OrdersPath}/{id}", body: null);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CafeOrderResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> PayAsync(
        HttpClient client, string token, Guid orderId, decimal amount) =>
            SendAsync(
                client,
                token,
                HttpMethod.Post,
                $"{OrdersPath}/{orderId}/payments",
                new { amount = Money(amount), method = "Cash", referenceNumber = (string?)null });

    private static async Task<PaymentResponse> PayOkAsync(
        HttpClient client, string token, Guid orderId, decimal amount)
    {
        using var response = await PayAsync(client, token, orderId, amount);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PaymentResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<MemberDebtResponse> DebtAsync(HttpClient client, string token, Guid memberId)
    {
        using var response = await SendAsync(
            client, token, HttpMethod.Get, $"/api/members/{memberId}/debt", body: null);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<MemberDebtResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task PostOkAsync(HttpClient client, string token, string path)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, path, body: null);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Money goes over the wire as a string, the way the frontend sends it: the allowed range has
    /// more digits than a JavaScript number holds exactly (docs/ARCHITECTURE.md).
    /// </summary>
    private static string Money(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client, string token, HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }

    private async Task<int> CountOrdersAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().CafeOrders
            .CountAsync(TestContext.Current.CancellationToken);
    }
}
