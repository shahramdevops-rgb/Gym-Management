using System.Globalization;
using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Attendances;
using Gym.Application.Attendances.AutoCheckout;
using Gym.Application.Cafe;
using Gym.Application.Common.Paging;
using Gym.Application.History.ListPayments;
using Gym.Application.History.ListSales;
using Gym.Application.History.ListServiceCharges;
using Gym.Application.Lockers;
using Gym.Application.Payments.SettleMemberDebt;
using Gym.Application.ServiceCharges;
using Gym.Domain.Payments;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Attendances;

/// <summary>
/// A guest's هوازی and sales (BUSINESS_RULES.md §7 <i>Guest visit</i>, roadmap 6.5.31): recorded on
/// the visit under the guest's name, on no account, and under the cafe's guest rule: check-out and a
/// cancel that would leave one unpaid are refused, auto-checkout leaves such a visit open, «تسویه
/// یکجا» pays them with the cafe, and the history names the guest. Run as Staff unless the test says
/// otherwise.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class GuestPurchasesEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    // ---- Recording ----

    [Fact]
    public async Task RecordServiceCharge_ClosedGuestVisit_Returns422VisitNotOpen()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);
        await PostOkAsync(client, token, CheckOutPath(visit.Id));

        using var response = await ChargeAsync(client, token, visit.Id, "Cardio", 30_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("ServiceCharges.VisitNotOpen");
    }

    [Fact]
    public async Task RecordServiceCharge_SecondCardioOnAGuestVisit_Returns409AlreadyCharged()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);
        await ChargeOkAsync(client, token, visit.Id, "Cardio", 30_000m);

        using var response = await ChargeAsync(client, token, visit.Id, "Cardio", 40_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("ServiceCharges.AlreadyCharged");
    }

    /// <summary>The guest's charge takes the visit's lock (a member's takes the member's); either way it works.</summary>
    [Fact]
    public async Task ChangeAmount_GuestsCardio_ChangesIt()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);
        var cardio = await ChargeOkAsync(client, token, visit.Id, "Cardio", 30_000m);

        using var response = await SendAsync(
            client, token, HttpMethod.Put, $"/api/service-charges/{cardio.Id}/amount", new { amount = 45_000m });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<ServiceChargeResponse>(response)).Amount.ShouldBe(45_000m);
    }

    [Fact]
    public async Task Void_PaidGuestCharge_VoidsItAndRefundsThePayment()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);
        var cardio = await ChargeOkAsync(client, token, visit.Id, "Cardio", 30_000m);
        await PostOkAsync(client, token, ChargePaymentsPath(cardio.Id), PaymentBody(30_000m));

        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/service-charges/{cardio.Id}/void", new { reason = "اشتباه ثبت شد" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<ServiceChargeResponse>(response)).VoidedAt.ShouldNotBeNull();
        (await NetPaidAsync(cardio.Id)).ShouldBe(0m);
    }

    // ---- Check-out and cancel ----

    [Theory]
    [InlineData("Cardio")]
    [InlineData("Analysis")]
    public async Task CheckOut_GuestWithAnUnpaidCharge_Returns422AndStaysInside(string kind)
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);
        await ChargeOkAsync(client, token, visit.Id, kind, 30_000m);

        using var response = await SendAsync(client, token, HttpMethod.Post, CheckOutPath(visit.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.GuestHasUnpaidPurchases");
        (await IsOpenAsync(visit.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task CheckOut_GuestWithAPartlyPaidShopItem_Returns422()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);
        var item = (await SellOkAsync(client, token, visit.Id, "دستکش", 1, 150_000m)).ShouldHaveSingleItem();
        await PostOkAsync(client, token, ChargePaymentsPath(item.Id), PaymentBody(100_000m));

        using var response = await SendAsync(client, token, HttpMethod.Post, CheckOutPath(visit.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.GuestHasUnpaidPurchases");
    }

    [Fact]
    public async Task CheckOut_GuestWhoseChargesWerePaidOrVoided_Succeeds()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);
        var cardio = await ChargeOkAsync(client, token, visit.Id, "Cardio", 30_000m);
        var analysis = await ChargeOkAsync(client, token, visit.Id, "Analysis", 200_000m);
        await PostOkAsync(client, token, ChargePaymentsPath(cardio.Id), PaymentBody(30_000m));
        await PostOkAsync(client, token, $"/api/service-charges/{analysis.Id}/void", new { reason = "انجام نشد" });

        using var response = await SendAsync(client, token, HttpMethod.Post, CheckOutPath(visit.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CancelCheckIn_GuestLeavingAnUnpaidCardioUnticked_Returns422AndStaysInside()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);
        await ChargeOkAsync(client, token, visit.Id, "Cardio", 30_000m);

        using var response = await SendAsync(
            client, token, HttpMethod.Post, CancelPath(visit.Id), CancelCheckInBody.KeepPurchases);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.GuestHasUnpaidPurchases");
        (await IsOpenAsync(visit.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task CancelCheckIn_GuestLeavingAnUnpaidSaleUnticked_Returns422()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);
        var cardio = await ChargeOkAsync(client, token, visit.Id, "Cardio", 30_000m);
        await ChargeOkAsync(client, token, visit.Id, "Analysis", 200_000m);

        // The هوازی is ticked, the آنالیز is not.
        using var response = await SendAsync(
            client, token, HttpMethod.Post, CancelPath(visit.Id), CancelCheckInBody.Cancel(voidCardio: true));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.GuestHasUnpaidPurchases");
        (await IsVoidedAsync(cardio.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task CancelCheckIn_GuestTickingEveryUnpaidCharge_CancelsTheVisitAndVoidsThem()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);
        var cardio = await ChargeOkAsync(client, token, visit.Id, "Cardio", 30_000m);
        var analysis = await ChargeOkAsync(client, token, visit.Id, "Analysis", 200_000m);

        using var response = await SendAsync(
            client,
            token,
            HttpMethod.Post,
            CancelPath(visit.Id),
            new { voidCardio = true, cafeOrderIds = Array.Empty<Guid>(), saleIds = new[] { analysis.Id } });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<AttendanceResponse>(response)).CancelledAt.ShouldNotBeNull();
        (await IsVoidedAsync(cardio.Id)).ShouldBeTrue();
        (await IsVoidedAsync(analysis.Id)).ShouldBeTrue();
    }

    // ---- Auto-checkout ----

    [Fact]
    public async Task AutoCheckout_GuestWithAnUnpaidCharge_LeavesTheVisitOpenAndTheLockerOwing()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token, lockerNumber: 7);
        await ChargeOkAsync(client, token, visit.Id, "Cardio", 30_000m);

        var closed = await RunAutoCheckoutAsync();

        closed.ShouldBe(0);
        (await IsOpenAsync(visit.Id)).ShouldBeTrue();
        var page = await GetOkAsync<PagedResponse<LockerResponse>>(client, token, $"/api/lockers?pageSize={PagingRules.MaxPageSize}");
        page.Items.Single(locker => locker.Number == 7).HolderDebt.ShouldBe(30_000m);
    }

    [Fact]
    public async Task AutoCheckout_GuestWhoPaidBesideOneWhoDidNot_ClosesOnlyThePaidVisit()
    {
        var (client, token) = await StaffClientAsync();
        var paidUp = await TestGuests.CheckInOkAsync(client, token, "مریم احمدی");
        var owing = await TestGuests.CheckInOkAsync(client, token, "سارا رضایی", lockerNumber: 2);
        var cardio = await ChargeOkAsync(client, token, paidUp.Id, "Cardio", 30_000m);
        await PostOkAsync(client, token, ChargePaymentsPath(cardio.Id), PaymentBody(30_000m));
        await SellOkAsync(client, token, owing.Id, "دستکش", 1, 150_000m);

        var closed = await RunAutoCheckoutAsync();

        closed.ShouldBe(1);
        (await IsOpenAsync(paidUp.Id)).ShouldBeFalse();
        (await IsOpenAsync(owing.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task AutoCheckout_GuestWhoseDebtWasSettledTheNextDay_ThenChecksOut()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);
        var analysis = await ChargeOkAsync(client, token, visit.Id, "Analysis", 200_000m);
        await RunAutoCheckoutAsync();

        await PostOkAsync(client, token, ChargePaymentsPath(analysis.Id), PaymentBody(200_000m));
        using var response = await SendAsync(client, token, HttpMethod.Post, CheckOutPath(visit.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await IsOpenAsync(visit.Id)).ShouldBeFalse();
    }

    // ---- تسویه یکجا ----

    [Fact]
    public async Task SettleGuest_CafeAndCharges_PaysEveryItemInOneSettlementAndThenCheckOutSucceeds()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);
        var order = await OrderOkAsync(client, token, visit.Id, 15_000m);
        var cardio = await ChargeOkAsync(client, token, visit.Id, "Cardio", 30_000m);
        var item = (await SellOkAsync(client, token, visit.Id, "دستکش", 2, 150_000m)).ShouldHaveSingleItem();
        await PostOkAsync(client, token, ChargePaymentsPath(item.Id), PaymentBody(100_000m));

        // 15,000 cafe + 30,000 هوازی + what is left of 300,000 on the item.
        using var response = await SendAsync(client, token, HttpMethod.Post, SettlePath(visit.Id), PaymentBody(245_000m, "Card"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var settlement = await ReadAsync<SettlementResponse>(response);
        settlement.Payments.Select(payment => (payment.Kind, payment.TargetId, payment.Amount)).ShouldBe(
            [
                (PaymentTargetKind.CafeOrder, order.Id, 15_000m),
                (PaymentTargetKind.ServiceCharge, cardio.Id, 30_000m),
                (PaymentTargetKind.ServiceCharge, item.Id, 200_000m),
            ]);
        settlement.RemainingDebt.ShouldBe(0m);
        (await SettlementIdsAsync(settlement.Payments.Select(payment => payment.PaymentId)))
            .ShouldHaveSingleItem().ShouldNotBeNull();

        using var checkOut = await SendAsync(client, token, HttpMethod.Post, CheckOutPath(visit.Id));
        checkOut.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SettleGuest_TheCafeTotalOnly_Returns409DebtChangedAndPaysNothing()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);
        var order = await OrderOkAsync(client, token, visit.Id, 15_000m);
        var cardio = await ChargeOkAsync(client, token, visit.Id, "Cardio", 30_000m);

        using var response = await SendAsync(client, token, HttpMethod.Post, SettlePath(visit.Id), PaymentBody(15_000m));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Settlements.DebtChanged");
        (await NetPaidAsync(cardio.Id)).ShouldBe(0m);
        (await GetOkAsync<CafeOrderResponse>(client, token, $"/api/cafe/orders/{order.Id}")).NetPaid.ShouldBe(0m);
    }

    [Fact]
    public async Task SettleGuest_OnlyCharges_PaysThem()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);
        var cardio = await ChargeOkAsync(client, token, visit.Id, "Cardio", 30_000m);

        using var response = await SendAsync(client, token, HttpMethod.Post, SettlePath(visit.Id), PaymentBody(30_000m));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await NetPaidAsync(cardio.Id)).ShouldBe(30_000m);
    }

    // ---- The map and the history ----

    [Fact]
    public async Task ListLockers_GuestWithAnUnpaidCardioAndOrder_HolderDebtIsBoth()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token, lockerNumber: 7);
        await OrderOkAsync(client, token, visit.Id, 15_000m);
        await ChargeOkAsync(client, token, visit.Id, "Cardio", 30_000m);

        var page = await GetOkAsync<PagedResponse<LockerResponse>>(client, token, $"/api/lockers?pageSize={PagingRules.MaxPageSize}");

        page.Items.Single(locker => locker.Number == 7).HolderDebt.ShouldBe(45_000m);
    }

    [Fact]
    public async Task History_GuestsCharge_IsUnderTheGuestsNameInEverySection()
    {
        var (client, token) = await OwnerClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token, "مریم احمدی");
        var cardio = await ChargeOkAsync(client, token, visit.Id, "Cardio", 30_000m);
        await PostOkAsync(client, token, ChargePaymentsPath(cardio.Id), PaymentBody(10_000m));

        var charge = (await GetOkAsync<PagedResponse<HistoryServiceChargeResponse>>(client, token, "/api/service-charges"))
            .Items.ShouldHaveSingleItem();
        charge.MemberId.ShouldBeNull();
        charge.MemberFullName.ShouldBeNull();
        charge.GuestName.ShouldBe("مریم احمدی");

        var payment = (await GetOkAsync<PagedResponse<HistoryPaymentResponse>>(client, token, "/api/payments"))
            .Items.ShouldHaveSingleItem();
        payment.MemberId.ShouldBeNull();
        payment.GuestName.ShouldBe("مریم احمدی");

        var sale = (await GetOkAsync<PagedResponse<HistorySaleResponse>>(client, token, "/api/sales?source=Cardio"))
            .Items.ShouldHaveSingleItem();
        sale.MemberId.ShouldBeNull();
        sale.GuestName.ShouldBe("مریم احمدی");
        sale.PaymentStatus.ShouldBe(PaymentStatus.Partial);
    }

    // ---- Helpers ----

    private static string CheckOutPath(Guid attendanceId) => $"/api/attendance/{attendanceId}/check-out";

    private static string CancelPath(Guid attendanceId) => $"/api/attendance/{attendanceId}/cancel";

    private static string SettlePath(Guid attendanceId) => $"/api/attendance/{attendanceId}/settle-guest";

    private static string ChargePaymentsPath(Guid chargeId) => $"/api/service-charges/{chargeId}/payments";

    private static object PaymentBody(decimal amount, string method = "Cash") =>
        new { amount = amount.ToString(CultureInfo.InvariantCulture), method, referenceNumber = (string?)null };

    private static Task<HttpResponseMessage> ChargeAsync(
        HttpClient client, string token, Guid attendanceId, string kind, decimal amount) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/attendance/{attendanceId}/service-charges", new { kind, amount });

    private static async Task<ServiceChargeResponse> ChargeOkAsync(
        HttpClient client, string token, Guid attendanceId, string kind, decimal amount)
    {
        using var response = await ChargeAsync(client, token, attendanceId, kind, amount);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await ReadAsync<ServiceChargeResponse>(response);
    }

    private static async Task<List<ServiceChargeResponse>> SellOkAsync(
        HttpClient client, string token, Guid attendanceId, string description, int quantity, decimal unitPrice)
    {
        using var response = await SendAsync(
            client,
            token,
            HttpMethod.Post,
            $"/api/attendance/{attendanceId}/service-charges/shop",
            new { items = new[] { new { description, quantity, unitPrice } } });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await ReadAsync<List<ServiceChargeResponse>>(response);
    }

    /// <summary>One product at <paramref name="price"/>, ordered on the guest's visit and left unpaid.</summary>
    private static async Task<CafeOrderResponse> OrderOkAsync(HttpClient client, string token, Guid attendanceId, decimal price)
    {
        var category = await PostOkAsync<ProductCategoryResponse>(
            client, token, "/api/cafe/categories", new { name = $"دسته {Guid.NewGuid():N}" });
        var product = await PostOkAsync<ProductResponse>(
            client,
            token,
            "/api/cafe/products",
            new { name = $"کالا {Guid.NewGuid():N}", categoryId = category.Id, price = price.ToString(CultureInfo.InvariantCulture) });

        using var response = await SendAsync(
            client,
            token,
            HttpMethod.Post,
            "/api/cafe/orders",
            new
            {
                memberId = (Guid?)null,
                attendanceId,
                items = new[] { new { productId = product.Id, quantity = 1 } },
                payment = (object?)null,
            });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await ReadAsync<CafeOrderResponse>(response);
    }

    private async Task<bool> IsOpenAsync(Guid attendanceId)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Attendances
            .AnyAsync(a => a.Id == attendanceId && a.CheckedOutAt == null, TestContext.Current.CancellationToken);
    }

    private async Task<int> RunAutoCheckoutAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AutoCheckoutHandler>()
            .Handle(TestContext.Current.CancellationToken);
    }

    private async Task<bool> IsVoidedAsync(Guid chargeId)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().ServiceCharges
            .AnyAsync(charge => charge.Id == chargeId && charge.VoidedAt != null, TestContext.Current.CancellationToken);
    }

    /// <summary>Payments less refunds against the charge.</summary>
    private async Task<decimal> NetPaidAsync(Guid chargeId)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Payments
            .Where(payment => payment.ServiceChargeId == chargeId)
            .SumAsync(
                payment => payment.Kind == PaymentKind.Payment ? payment.Amount : -payment.Amount,
                TestContext.Current.CancellationToken);
    }

    /// <summary>The distinct settlement ids of these payments: one, when they were one handover.</summary>
    private async Task<List<Guid?>> SettlementIdsAsync(IEnumerable<Guid> paymentIds)
    {
        var wanted = paymentIds.ToList();
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Payments
            .Where(payment => wanted.Contains(payment.Id))
            .Select(payment => payment.SettlementId)
            .Distinct()
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<(HttpClient Client, string Token)> StaffClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    /// <summary>The sales list is the Owner's, and the Owner reads payments with no date limit (§12).</summary>
    private async Task<(HttpClient Client, string Token)> OwnerClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("owner", TestUsers.Password));
    }

    private static async Task PostOkAsync(HttpClient client, string token, string path, object? body = null)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, path, body);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<T> PostOkAsync<T>(HttpClient client, string token, string path, object body)
        where T : class
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, path, body);
        response.EnsureSuccessStatusCode();

        return await ReadAsync<T>(response);
    }

    private static async Task<T> GetOkAsync<T>(HttpClient client, string token, string path)
        where T : class
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, path);
        response.EnsureSuccessStatusCode();

        return await ReadAsync<T>(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
        where T : class =>
        (await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

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
