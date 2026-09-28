using System.Globalization;
using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Cafe;
using Gym.Application.Common.Paging;
using Gym.Application.Members.GetMemberDebt;
using Gym.Application.Payments;
using Gym.Domain.Payments;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Cafe;

/// <summary>
/// <c>/api/cafe/orders</c> (BUSINESS_RULES.md §8, tasks 7.2 and 7.3). Nothing here counts stock:
/// the point of these tests is the money — the price snapshot, the walk-in rule, an order on a
/// member's account turning into their debt, and cancelling giving back what was paid.
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
    public async Task MemberDebt_UnpaidOrderOnAccount_ListsWhatItBoughtWithPrices()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var cake = await AddProductAsync(client, token, "کیک", 40_000m);
        await CreateOkAsync(client, token, member.Id, [(water.Id, 2), (cake.Id, 1)], paid: null);

        var debt = await DebtAsync(client, token, member.Id);

        var lines = debt.Items.ShouldHaveSingleItem().CafeItems;
        lines.Count.ShouldBe(2);
        lines.ShouldContain(line => line.ProductName == "آب معدنی" && line.Quantity == 2 && line.LineTotal == 30_000m);
        lines.ShouldContain(line => line.ProductName == "کیک" && line.Quantity == 1 && line.LineTotal == 40_000m);
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

    // ---- Cancelling ----

    [Fact]
    public async Task CancelOrder_UnpaidOnAccountByStaff_LeavesTheDebtAndRefundsNothing()
    {
        // An unpaid order on account leaves nothing to refund; cancelling it simply removes it
        // from the member's debt (BUSINESS_RULES.md §8). Staff may do it (§1, §8).
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var order = await CreateOkAsync(client, token, member.Id, [(water.Id, 2)], paid: null);

        var cancelled = await CancelOkAsync(client, token, order.Id, "  دو بار زده شد  ");

        cancelled.CancelledAt.ShouldNotBeNull();
        cancelled.CancelReason.ShouldBe("دو بار زده شد");
        cancelled.Outstanding.ShouldBe(0m);
        (await DebtAsync(client, token, member.Id)).Total.ShouldBe(0m);
        (await PaymentsOfAsync(order.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task CancelOrder_PaidWalkIn_RefundsTheWholeAmountWithTheReason()
    {
        var (client, token) = await StaffClientAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var order = await CreateOkAsync(client, token, memberId: null, [(water.Id, 2)], paid: 30_000m);

        var cancelled = await CancelOkAsync(client, token, order.Id, "مشتری منصرف شد");

        cancelled.NetPaid.ShouldBe(0m);
        var refund = (await PaymentsOfAsync(order.Id)).Where(p => p.Kind == PaymentKind.Refund).ShouldHaveSingleItem();
        refund.Amount.ShouldBe(30_000m);
        refund.Method.ShouldBe(PaymentMethod.Cash);
        refund.Reason.ShouldBe("مشتری منصرف شد");
        (await GetOrderAsync(client, token, order.Id)).NetPaid.ShouldBe(0m);
    }

    [Fact]
    public async Task CancelOrder_PaidInCashAndByCard_RefundsEachMethodSeparately()
    {
        // Money goes back the way it came, as with a voided service charge (BUSINESS_RULES.md §8).
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var order = await CreateOkAsync(client, token, member.Id, [(water.Id, 3)], paid: 10_000m);
        await PayOkAsync(client, token, order.Id, 20_000m, method: "Card");

        await CancelOkAsync(client, token, order.Id, "اشتباه در سفارش");

        var refunds = (await PaymentsOfAsync(order.Id)).Where(p => p.Kind == PaymentKind.Refund).ToList();
        refunds.Count.ShouldBe(2);
        refunds.Single(r => r.Method == PaymentMethod.Cash).Amount.ShouldBe(10_000m);
        refunds.Single(r => r.Method == PaymentMethod.Card).Amount.ShouldBe(20_000m);
    }

    [Fact]
    public async Task CancelOrder_Twice_Returns422AndRefundsOnlyOnce()
    {
        var (client, token) = await StaffClientAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var order = await CreateOkAsync(client, token, memberId: null, [(water.Id, 1)], paid: 15_000m);
        await CancelOkAsync(client, token, order.Id, "اول");

        using var response = await CancelAsync(client, token, order.Id, "دوم");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("CafeOrders.AlreadyCancelled");
        (await PaymentsOfAsync(order.Id)).Count(p => p.Kind == PaymentKind.Refund).ShouldBe(1);
        (await GetOrderAsync(client, token, order.Id)).CancelReason.ShouldBe("اول");
    }

    [Fact]
    public async Task CancelOrder_BlankReason_Returns400AndChangesNothing()
    {
        var (client, token) = await StaffClientAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var order = await CreateOkAsync(client, token, memberId: null, [(water.Id, 1)], paid: 15_000m);

        using var response = await CancelAsync(client, token, order.Id, "   ");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await GetOrderAsync(client, token, order.Id)).CancelledAt.ShouldBeNull();
    }

    [Fact]
    public async Task CancelOrder_UnknownOrder_Returns404()
    {
        var (client, token) = await StaffClientAsync();

        using var response = await CancelAsync(client, token, Guid.CreateVersion7(), "دلیل");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("CafeOrders.NotFound");
    }

    [Fact]
    public async Task CancelOrder_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"{OrdersPath}/{Guid.CreateVersion7()}/cancel", new { reason = "دلیل" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PayOrder_AfterItWasCancelled_Returns422()
    {
        // A cancelled order owes nothing, so there is nothing to pay against it.
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var order = await CreateOkAsync(client, token, member.Id, [(water.Id, 1)], paid: null);
        await CancelOkAsync(client, token, order.Id, "دلیل");

        using var response = await PayAsync(client, token, order.Id, 15_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("CafeOrders.AlreadyCancelled");
    }

    [Fact]
    public async Task CancelOrder_InParallel_CancelsOnceAndRefundsOnce()
    {
        // A walk-in order has no member row to lock; the order's xmin is what stops the second
        // cancel, and its refund is rolled back with it.
        var (client, token) = await StaffClientAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var order = await CreateOkAsync(client, token, memberId: null, [(water.Id, 2)], paid: 30_000m);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(i => CancelAsync(client, token, order.Id, $"دلیل {i}")));

        try
        {
            responses.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBe(1);
            responses.ShouldAllBe(response =>
                response.StatusCode == HttpStatusCode.OK ||
                response.StatusCode == HttpStatusCode.Conflict ||
                response.StatusCode == HttpStatusCode.UnprocessableEntity);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        var refunds = (await PaymentsOfAsync(order.Id)).Where(p => p.Kind == PaymentKind.Refund).ToList();
        refunds.Sum(r => r.Amount).ShouldBe(30_000m);
    }

    [Fact]
    public async Task CancelAndPay_InParallel_NeverLeavesMoneyOnACancelledOrder()
    {
        // The payment asks "is it cancelled?" again under the member lock the cancel also takes,
        // so whichever runs second sees the first: either the payment is refunded by the cancel,
        // or the payment is refused.
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var order = await CreateOkAsync(client, token, member.Id, [(water.Id, 1)], paid: null);

            var cancel = CancelAsync(client, token, order.Id, "دلیل");
            var pay = PayAsync(client, token, order.Id, 15_000m);
            using var cancelResponse = await cancel;
            using var payResponse = await pay;

            cancelResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await GetOrderAsync(client, token, order.Id)).NetPaid.ShouldBe(0m);
        }
    }

    [Fact]
    public async Task MemberPayments_CancelledPaidOrder_ShowsThePaymentAndItsRefund()
    {
        // Cafe payments belong to the member's payment history like any other (BUSINESS_RULES.md §5).
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var order = await CreateOkAsync(client, token, member.Id, [(water.Id, 1)], paid: 15_000m);
        await CancelOkAsync(client, token, order.Id, "دلیل");

        using var response = await SendAsync(
            client, token, HttpMethod.Get, $"/api/members/{member.Id}/payments", body: null);
        response.EnsureSuccessStatusCode();
        var page = (await response.Content.ReadFromJsonAsync<PagedResponse<PaymentHistoryResponse>>(
            TestContext.Current.CancellationToken)).ShouldNotBeNull();

        page.TotalCount.ShouldBe(2);
        page.Items.ShouldAllBe(row => row.TargetKind == PaymentTargetKind.CafeOrder && row.TargetId == order.Id);
        page.Items.Select(row => row.Kind).ShouldBe([PaymentKind.Payment, PaymentKind.Refund], ignoreOrder: true);
    }

    // ---- History ----

    [Fact]
    public async Task ListOrders_NewestFirst_IncludesCancelledOnesWithTheirLines()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var first = await CreateOkAsync(client, token, memberId: null, [(water.Id, 1)], paid: 15_000m);
        var second = await CreateOkAsync(client, token, member.Id, [(water.Id, 2)], paid: 10_000m);
        await CancelOkAsync(client, token, first.Id, "دلیل");

        var page = await ListOkAsync(client, token, OrdersPath);

        page.TotalCount.ShouldBe(2);
        page.Items.Select(order => order.Id).ShouldBe([second.Id, first.Id]);

        var onAccount = page.Items[0];
        onAccount.MemberFullName.ShouldBe(member.FullName);
        onAccount.NetPaid.ShouldBe(10_000m);
        onAccount.Outstanding.ShouldBe(20_000m);
        onAccount.Items.ShouldHaveSingleItem().Quantity.ShouldBe(2);

        var walkIn = page.Items[1];
        walkIn.CancelledAt.ShouldNotBeNull();
        walkIn.NetPaid.ShouldBe(0m);
    }

    [Fact]
    public async Task ListOrders_ByMember_ReturnsOnlyThatMembersOrders()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var other = await AddMemberAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var mine = await CreateOkAsync(client, token, member.Id, [(water.Id, 1)], paid: null);
        await CreateOkAsync(client, token, other.Id, [(water.Id, 1)], paid: null);
        await CreateOkAsync(client, token, memberId: null, [(water.Id, 1)], paid: 15_000m);

        var byQuery = await ListOkAsync(client, token, $"{OrdersPath}?memberId={member.Id}");
        var byMember = await ListOkAsync(client, token, $"/api/members/{member.Id}/cafe-orders");

        byQuery.Items.ShouldHaveSingleItem().Id.ShouldBe(mine.Id);
        byMember.Items.ShouldHaveSingleItem().Id.ShouldBe(mine.Id);
    }

    [Fact]
    public async Task ListOrders_DateRange_FiltersByTheBusinessDateInclusively()
    {
        var (client, token) = await StaffClientAsync();
        var water = await AddProductAsync(client, token, "آب معدنی", 15_000m);
        var before = await CreateOkAsync(client, token, memberId: null, [(water.Id, 1)], paid: 15_000m);
        var onStart = await CreateOkAsync(client, token, memberId: null, [(water.Id, 1)], paid: 15_000m);
        var onEnd = await CreateOkAsync(client, token, memberId: null, [(water.Id, 1)], paid: 15_000m);
        var after = await CreateOkAsync(client, token, memberId: null, [(water.Id, 1)], paid: 15_000m);
        await SetOrderedOnAsync(before.Id, new DateOnly(2026, 9, 9));
        await SetOrderedOnAsync(onStart.Id, new DateOnly(2026, 9, 10));
        await SetOrderedOnAsync(onEnd.Id, new DateOnly(2026, 9, 12));
        await SetOrderedOnAsync(after.Id, new DateOnly(2026, 9, 13));

        var page = await ListOkAsync(client, token, $"{OrdersPath}?from=2026-09-10&to=2026-09-12");

        page.Items.Select(order => order.Id).ShouldBe([onEnd.Id, onStart.Id]);
    }

    [Fact]
    public async Task ListOrders_FromAfterTo_Returns400()
    {
        var (client, token) = await StaffClientAsync();

        using var response = await SendAsync(
            client, token, HttpMethod.Get, $"{OrdersPath}?from=2026-09-12&to=2026-09-10", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListMemberOrders_UnknownMember_Returns404()
    {
        var (client, token) = await StaffClientAsync();

        using var response = await SendAsync(
            client, token, HttpMethod.Get, $"/api/members/{Guid.CreateVersion7()}/cafe-orders", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Members.NotFound");
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
        HttpClient client, string token, Guid orderId, decimal amount, string method = "Cash") =>
            SendAsync(
                client,
                token,
                HttpMethod.Post,
                $"{OrdersPath}/{orderId}/payments",
                new { amount = Money(amount), method, referenceNumber = (string?)null });

    private static async Task<PaymentResponse> PayOkAsync(
        HttpClient client, string token, Guid orderId, decimal amount, string method = "Cash")
    {
        using var response = await PayAsync(client, token, orderId, amount, method);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PaymentResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> CancelAsync(
        HttpClient client, string token, Guid orderId, string reason) =>
            SendAsync(client, token, HttpMethod.Post, $"{OrdersPath}/{orderId}/cancel", new { reason });

    private static async Task<CafeOrderResponse> CancelOkAsync(
        HttpClient client, string token, Guid orderId, string reason)
    {
        using var response = await CancelAsync(client, token, orderId, reason);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CafeOrderResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<PagedResponse<CafeOrderResponse>> ListOkAsync(
        HttpClient client, string token, string path)
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, path, body: null);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PagedResponse<CafeOrderResponse>>(
            TestContext.Current.CancellationToken)).ShouldNotBeNull();
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

    private async Task<List<Payment>> PaymentsOfAsync(Guid orderId)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Payments
            .AsNoTracking()
            .Where(payment => payment.CafeOrderId == orderId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Moves an order to another business day. The tests run on the real clock, so every order is
    /// rung up today; the history's date filter needs orders on other days.
    /// </summary>
    private async Task SetOrderedOnAsync(Guid orderId, DateOnly orderedOn)
    {
        await using var scope = Fixture.CreateScope();

        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"UPDATE cafe_orders SET ordered_on = {orderedOn} WHERE id = {orderId}",
            TestContext.Current.CancellationToken);
    }
}
