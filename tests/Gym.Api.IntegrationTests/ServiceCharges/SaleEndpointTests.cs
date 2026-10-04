using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Attendances;
using Gym.Application.Common.Paging;
using Gym.Application.History.ListPayments;
using Gym.Application.Members.GetMemberDebt;
using Gym.Application.Payments.SettleMemberDebt;
using Gym.Application.ServiceCharges;
using Gym.Domain.Members;
using Gym.Domain.Payments;
using Gym.Domain.ServiceCharges;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Gym.Api.IntegrationTests.ServiceCharges;

/// <summary>
/// «فروشگاه» and «آنالیز» end to end (BUSINESS_RULES.md §7 <i>Sale at the desk</i>, tasks 6.5.28
/// and 6.5.29): a فروشگاه sale of one or more named items, or an آنالیز of a single price, put on
/// the member's account and paid afterwards, any number per visit, voided rather than edited, and
/// each listed as its own source.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SaleEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static int _phoneSuffix;

    // ---- Recording فروشگاه ----

    /// <summary>No money is taken: the item goes straight onto the member's debt (decided 1405/07/12).</summary>
    [Fact]
    public async Task Shop_OneItem_CreatesAnUnpaidChargeInTheMembersDebt()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);

        using var response = await SellAsync(client, token, visit.Id, Item("  دستکش  ", 2, 150_000m));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sale = (await ReadChargesAsync(response)).ShouldHaveSingleItem();
        sale.Kind.ShouldBe(ServiceChargeKind.Miscellaneous);
        sale.Description.ShouldBe("دستکش");
        sale.Quantity.ShouldBe(2);
        sale.UnitPrice.ShouldBe(150_000m);
        sale.Amount.ShouldBe(300_000m);
        sale.NetPaid.ShouldBe(0m);
        sale.PaymentStatus.ShouldBe(PaymentStatus.Unpaid);
        sale.CanChangeAmount.ShouldBeFalse();
        (await StoredPaymentsAsync(sale.Id)).ShouldBeEmpty();

        var debtItem = (await GetDebtOkAsync(client, token, visit.MemberId!.Value)).Items
            .Single(item => item.Id == sale.Id);
        debtItem.ServiceKind.ShouldBe(ServiceChargeKind.Miscellaneous);
        debtItem.Sale.ShouldBe(new SaleSummary("دستکش", 2, 150_000m));
        debtItem.Outstanding.ShouldBe(300_000m);
    }

    /// <summary>Two things sold in one go: each is its own charge, so each is paid or voided on its own.</summary>
    [Fact]
    public async Task Shop_TwoItems_RecordsOneChargeEach()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);

        var sales = await SellOkAsync(client, token, visit.Id, Item("دستکش", 2, 150_000m), Item("حوله", 1, 80_000m));

        sales.Select(sale => (sale.Description, sale.Amount)).ShouldBe([("دستکش", 300_000m), ("حوله", 80_000m)]);
        (await GetDebtOkAsync(client, token, visit.MemberId!.Value)).Items
            .Where(item => item.ServiceKind == ServiceChargeKind.Miscellaneous)
            .Sum(item => item.Outstanding).ShouldBe(380_000m);
    }

    /// <summary>One bad item refuses the whole sale, so the desk never sees half of it saved.</summary>
    [Fact]
    public async Task Shop_OneBadItem_Returns400AndSavesNothing()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);

        using var response = await SellAsync(client, token, visit.Id, Item("دستکش", 1, 150_000m), Item("حوله", 0, 80_000m));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorCodesAsync(response)).ShouldContain("ServiceCharges.QuantityInvalid");
        (await StoredChargesOfVisitAsync(visit.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Shop_NoItems_Returns400ShopItemsRequired()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);

        using var response = await SellAsync(client, token, visit.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorCodesAsync(response)).ShouldContain("ServiceCharges.ShopItemsRequired");
    }

    [Theory]
    [InlineData("   ", 1, 10_000, "ServiceCharges.DescriptionRequired")]
    [InlineData("دستکش", 0, 10_000, "ServiceCharges.QuantityInvalid")]
    [InlineData("دستکش", 1000, 10_000, "ServiceCharges.QuantityInvalid")]
    [InlineData("دستکش", 1, 0, "ServiceCharges.AmountNotPositive")]
    public async Task Shop_InvalidField_Returns400WithItsCode(string description, int quantity, int unitPrice, string code)
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);

        using var response = await SellAsync(client, token, visit.Id, Item(description, quantity, unitPrice));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorCodesAsync(response)).ShouldContain(code);
    }

    /// <summary>Any number per visit (decided with the developer, 1405/07/11), beside one هوازی.</summary>
    [Fact]
    public async Task Record_SeveralOnTheSameVisitBesideCardio_AllStand()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        await ChargeOkAsync(client, token, visit.Id, "Cardio", 40_000m);

        await SellOkAsync(client, token, visit.Id, Item("دستکش", 1, 150_000m));
        await SellOkAsync(client, token, visit.Id, Item("دستکش", 1, 150_000m));
        await ChargeOkAsync(client, token, visit.Id, "Analysis", 200_000m);
        await ChargeOkAsync(client, token, visit.Id, "Analysis", 200_000m);

        (await GetDebtOkAsync(client, token, visit.MemberId!.Value)).Items
            .Count(item => item.Kind == PaymentTargetKind.ServiceCharge).ShouldBe(5);
    }

    /// <summary>A guest's visit too since task 6.5.31: under their name, on no account.</summary>
    [Fact]
    public async Task Shop_GuestVisit_RecordsTheItemsWithNoMember()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);

        var sold = await SellOkAsync(client, token, visit.Id, Item("دستکش", 2, 150_000m));

        var item = sold.ShouldHaveSingleItem();
        item.MemberId.ShouldBeNull();
        item.AttendanceId.ShouldBe(visit.Id);
        item.Amount.ShouldBe(300_000m);
    }

    [Fact]
    public async Task Shop_ClosedVisit_Returns422ServiceChargesVisitNotOpen()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        using (var checkedOut = await SendAsync(client, token, HttpMethod.Post, $"/api/attendance/{visit.Id}/check-out"))
        {
            checkedOut.EnsureSuccessStatusCode();
        }

        using var response = await SellAsync(client, token, visit.Id, Item("دستکش", 1, 150_000m));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("ServiceCharges.VisitNotOpen");
    }

    [Fact]
    public async Task Shop_UnknownVisit_Returns404()
    {
        var (client, token) = await StaffClientAsync();

        using var response = await SellAsync(client, token, Guid.CreateVersion7(), Item("دستکش", 1, 150_000m));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>The single-amount endpoint cannot make a فروشگاه item without its name and quantity.</summary>
    [Fact]
    public async Task RecordServiceCharge_MiscellaneousKind_Returns400ServiceChargesKindInvalid()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);

        using var response = await ChargeAsync(client, token, visit.Id, "Miscellaneous", 10_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorCodeAsync(response, "kind")).ShouldBe("ServiceCharges.KindInvalid");
    }

    // ---- Recording آنالیز ----

    /// <summary>Task 6.5.29: آنالیز is only a price, put on the member's debt.</summary>
    [Fact]
    public async Task Analysis_Price_CreatesAnUnpaidChargeWithNoNameOrQuantity()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);

        var analysis = await ChargeOkAsync(client, token, visit.Id, "Analysis", 200_000m);

        analysis.Kind.ShouldBe(ServiceChargeKind.Analysis);
        analysis.Amount.ShouldBe(200_000m);
        analysis.Description.ShouldBeNull();
        analysis.Quantity.ShouldBeNull();
        analysis.PaymentStatus.ShouldBe(PaymentStatus.Unpaid);
        analysis.CanChangeAmount.ShouldBeFalse();
        var debtItem = (await GetDebtOkAsync(client, token, visit.MemberId!.Value)).Items
            .Single(item => item.Id == analysis.Id);
        debtItem.ServiceKind.ShouldBe(ServiceChargeKind.Analysis);
        debtItem.Sale.ShouldBeNull();
        debtItem.Outstanding.ShouldBe(200_000m);
    }

    [Fact]
    public async Task Analysis_GuestVisit_RecordsItWithNoMember()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);

        var analysis = await ChargeOkAsync(client, token, visit.Id, "Analysis", 200_000m);

        analysis.MemberId.ShouldBeNull();
        analysis.AttendanceId.ShouldBe(visit.Id);
    }

    /// <summary>
    /// The database refuses a فروشگاه item with no name, or one whose total is not what its
    /// quantity and unit price make, and an آنالیز that carries a name, even if the code above it
    /// were bypassed.
    /// </summary>
    [Theory]
    [InlineData("Miscellaneous", "description = NULL")]
    [InlineData("Miscellaneous", "amount = 1")]
    [InlineData("Miscellaneous", "quantity = 0, amount = 0")]
    [InlineData("Miscellaneous", "kind = 'Analysis'")]
    [InlineData("Analysis", "description = 'x', quantity = 1, unit_price = 200000")]
    public async Task ServiceCharge_SaleBrokenByHand_RejectedByACheckConstraint(string kind, string change)
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var sale = kind == "Analysis"
            ? await ChargeOkAsync(client, token, visit.Id, "Analysis", 200_000m)
            : (await SellOkAsync(client, token, visit.Id, Item("دستکش", 2, 150_000m))).Single();
        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteSqlAsync($"UPDATE service_charges SET {change} WHERE id = '{sale.Id}'"));
        exception.SqlState.ShouldBe("23514");
    }

    // ---- Correcting ----

    [Theory]
    [InlineData("Miscellaneous")]
    [InlineData("Analysis")]
    public async Task ChangeAmount_Sale_Returns422NotEditable(string kind)
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var sale = kind == "Analysis"
            ? await ChargeOkAsync(client, token, visit.Id, "Analysis", 150_000m)
            : (await SellOkAsync(client, token, visit.Id, Item("دستکش", 1, 150_000m))).Single();

        using var response = await SendAsync(
            client, token, HttpMethod.Put, $"/api/service-charges/{sale.Id}/amount", new { amount = 100_000m });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("ServiceCharges.SaleNotEditable");
        (await StoredChargeAsync(sale.Id)).Amount.ShouldBe(150_000m);
    }

    /// <summary>Paid afterwards, then voided: the money goes back the way it came.</summary>
    [Fact]
    public async Task Void_PaidSale_RefundsTheMoneyTheWayItCame()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var sale = (await SellOkAsync(client, token, visit.Id, Item("دستکش", 1, 150_000m))).Single();
        await PayOkAsync(client, token, sale.Id, 150_000m, "Cash");

        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/service-charges/{sale.Id}/void", new { reason = "اشتباه ثبت شد" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var refund = (await StoredPaymentsAsync(sale.Id)).Single(payment => payment.Kind == PaymentKind.Refund);
        refund.Amount.ShouldBe(150_000m);
        refund.Method.ShouldBe(PaymentMethod.Cash);
    }

    // ---- Cancelling the check-in ----

    /// <summary>Each sale has its own tick, like a cafe order; only the ticked one is voided.</summary>
    [Fact]
    public async Task CancelCheckIn_OneSaleTicked_VoidsOnlyThatOneAndRefundsIt()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var sales = await SellOkAsync(client, token, visit.Id, Item("دستکش", 1, 150_000m), Item("حوله", 1, 50_000m));
        var (ticked, kept) = (sales[0], sales[1]);
        await PayOkAsync(client, token, ticked.Id, 150_000m, "Card");

        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/attendance/{visit.Id}/cancel", CancelCheckInBody.VoidSales(ticked.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAttendanceAsync(response)).ServiceCharges.Select(charge => charge.Id).ShouldBe([kept.Id]);
        var voided = await StoredChargeAsync(ticked.Id);
        voided.VoidReason.ShouldBe("The check-in was cancelled.");
        (await StoredPaymentsAsync(ticked.Id)).Sum(p => p.Kind == PaymentKind.Payment ? p.Amount : -p.Amount).ShouldBe(0m);
        (await StoredChargeAsync(kept.Id)).IsVoided.ShouldBeFalse();
    }

    /// <summary>One list of ticks covers both kinds: a فروشگاه item and an آنالیز go together.</summary>
    [Fact]
    public async Task CancelCheckIn_SaleOfEachKindTicked_VoidsBoth()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var shop = (await SellOkAsync(client, token, visit.Id, Item("دستکش", 1, 150_000m))).Single();
        var analysis = await ChargeOkAsync(client, token, visit.Id, "Analysis", 200_000m);
        await PayOkAsync(client, token, analysis.Id, 200_000m, "Cash");

        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/attendance/{visit.Id}/cancel", CancelCheckInBody.VoidSales(shop.Id, analysis.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAttendanceAsync(response)).ServiceCharges.ShouldBeEmpty();
        (await StoredChargeAsync(shop.Id)).IsVoided.ShouldBeTrue();
        (await StoredChargeAsync(analysis.Id)).IsVoided.ShouldBeTrue();
        (await StoredPaymentsAsync(analysis.Id)).Sum(p => p.Kind == PaymentKind.Payment ? p.Amount : -p.Amount).ShouldBe(0m);
    }

    [Fact]
    public async Task CancelCheckIn_AnotherVisitsSale_Returns422AndCancelsNothing()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var other = await CheckedInMemberAsync(client, token, lockerNumber: 2);
        var othersSale = (await SellOkAsync(client, token, other.Id, Item("دستکش", 1, 150_000m))).Single();

        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/attendance/{visit.Id}/cancel", CancelCheckInBody.VoidSales(othersSale.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.SaleNotOnVisit");
        (await StoredChargeAsync(othersSale.Id)).IsVoided.ShouldBeFalse();
        await using var scope = Fixture.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Attendances.AsNoTracking()
            .SingleAsync(a => a.Id == visit.Id, TestContext.Current.CancellationToken)).CheckedOutAt.ShouldBeNull();
    }

    // ---- Settling and history ----

    /// <summary>
    /// §5 <i>Settling several items at once</i>: a sale is a service charge, so it is paid after
    /// the cafe and before the plan.
    /// </summary>
    [Fact]
    public async Task Settle_LessThanTheWhole_PaysTheSaleBeforeThePlan()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var sale = (await SellOkAsync(client, token, visit.Id, Item("دستکش", 1, 150_000m))).Single();
        var debt = await GetDebtOkAsync(client, token, visit.MemberId!.Value);

        using var response = await SendAsync(client, token, HttpMethod.Post, $"/api/members/{visit.MemberId}/settlements", new
        {
            amount = 150_000m,
            method = "Cash",
            items = debt.Items.Select(item => new { kind = item.Kind.ToString(), id = item.Id, outstanding = item.Outstanding }),
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var settlement = (await response.Content.ReadFromJsonAsync<SettlementResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        var paid = settlement.Payments.ShouldHaveSingleItem();
        paid.TargetId.ShouldBe(sale.Id);
        paid.Outstanding.ShouldBe(0m);
    }

    /// <summary>The history lists هوازی, فروشگاه and آنالیز as separate sources (decided with the developer, 1405/07/11).</summary>
    [Theory]
    [InlineData(ServiceChargeKind.Miscellaneous)]
    [InlineData(ServiceChargeKind.Analysis)]
    public async Task ListPayments_SaleSource_ListsOnlyThatKindsPayments(ServiceChargeKind kind)
    {
        var (client, token) = await OwnerClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var shop = (await SellOkAsync(client, token, visit.Id, Item("دستکش", 1, 150_000m))).Single();
        var analysis = await ChargeOkAsync(client, token, visit.Id, "Analysis", 200_000m);
        var cardio = await ChargeOkAsync(client, token, visit.Id, "Cardio", 40_000m);
        await PayOkAsync(client, token, shop.Id, 150_000m, "Cash");
        await PayOkAsync(client, token, analysis.Id, 200_000m, "Card");
        await PayOkAsync(client, token, cardio.Id, 40_000m, "Cash");

        using var response = await SendAsync(
            client, token, HttpMethod.Get, $"/api/payments?source=ServiceCharge&serviceKind={kind}");

        response.EnsureSuccessStatusCode();
        var page = (await response.Content.ReadFromJsonAsync<PagedResponse<HistoryPaymentResponse>>(
            TestContext.Current.CancellationToken)).ShouldNotBeNull();
        var row = page.Items.ShouldHaveSingleItem();
        row.TargetId.ShouldBe(kind == ServiceChargeKind.Analysis ? analysis.Id : shop.Id);
        row.ServiceKind.ShouldBe(kind);
        row.ServiceDescription.ShouldBe(kind == ServiceChargeKind.Analysis ? null : "دستکش");
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

    /// <summary>A member with a subscription, checked in on the locker given.</summary>
    private async Task<AttendanceResponse> CheckedInMemberAsync(HttpClient client, string token, int lockerNumber = 1)
    {
        var member = await AddMemberAsync();
        var plan = await TestPlans.AddAsync(Fixture);

        using var assigned = await SendAsync(
            client, token, HttpMethod.Post, $"/api/members/{member.Id}/subscriptions", plan.Body);
        assigned.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var response = await TestLockers.CheckInAsync(client, token, member.Id, lockerNumber);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await ReadAttendanceAsync(response);
    }

    private async Task<Member> AddMemberAsync()
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed("رضا احمدی", $"+98914{suffix:D7}");

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task<ServiceCharge> StoredChargeAsync(Guid id)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().ServiceCharges
            .AsNoTracking()
            .SingleAsync(charge => charge.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task<List<Payment>> StoredPaymentsAsync(Guid serviceChargeId)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Payments
            .AsNoTracking()
            .Where(payment => payment.ServiceChargeId == serviceChargeId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<ServiceCharge>> StoredChargesOfVisitAsync(Guid attendanceId)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().ServiceCharges
            .AsNoTracking()
            .Where(charge => charge.AttendanceId == attendanceId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private static object Item(string description, int quantity, decimal unitPrice) =>
        new { description, quantity, unitPrice };

    /// <summary>A «فروشگاه» sale of the items given.</summary>
    private static Task<HttpResponseMessage> SellAsync(
        HttpClient client, string token, Guid attendanceId, params object[] items) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/attendance/{attendanceId}/service-charges/shop", new { items });

    private static async Task<List<ServiceChargeResponse>> SellOkAsync(
        HttpClient client, string token, Guid attendanceId, params object[] items)
    {
        using var response = await SellAsync(client, token, attendanceId, items);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await ReadChargesAsync(response);
    }

    /// <summary>A single-amount charge: هوازی or آنالیز.</summary>
    private static Task<HttpResponseMessage> ChargeAsync(
        HttpClient client, string token, Guid attendanceId, string kind, decimal amount) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/attendance/{attendanceId}/service-charges", new { kind, amount });

    private static async Task<ServiceChargeResponse> ChargeOkAsync(
        HttpClient client, string token, Guid attendanceId, string kind, decimal amount)
    {
        using var response = await ChargeAsync(client, token, attendanceId, kind, amount);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await ReadChargeAsync(response);
    }

    private static async Task PayOkAsync(HttpClient client, string token, Guid serviceChargeId, decimal amount, string method)
    {
        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/service-charges/{serviceChargeId}/payments", new { amount, method });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    /// <summary>Every error code in a validation response, whichever field (or list item) it is on.</summary>
    private static async Task<List<string>> ErrorCodesAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return body.RootElement.GetProperty("errors").EnumerateObject()
            .SelectMany(field => field.Value.EnumerateArray())
            .Select(error => error.GetProperty("code").GetString()!)
            .ToList();
    }

    private static async Task<List<ServiceChargeResponse>> ReadChargesAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<List<ServiceChargeResponse>>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

    private static async Task<MemberDebtResponse> GetDebtOkAsync(HttpClient client, string token, Guid memberId)
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, $"/api/members/{memberId}/debt");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<MemberDebtResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<string?> FieldErrorCodeAsync(HttpResponseMessage response, string field)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return body.RootElement.GetProperty("errors").GetProperty(field)[0].GetProperty("code").GetString();
    }

    private static async Task<ServiceChargeResponse> ReadChargeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ServiceChargeResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

    private static async Task<AttendanceResponse> ReadAttendanceAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<AttendanceResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

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
