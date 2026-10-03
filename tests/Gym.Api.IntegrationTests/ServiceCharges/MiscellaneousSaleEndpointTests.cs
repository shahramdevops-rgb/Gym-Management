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
/// «متفرقه» end to end (BUSINESS_RULES.md §7 <i>Miscellaneous sale</i>, task 6.5.28): a sale the
/// desk names itself, paid there and then or left on the member's account, any number per visit,
/// voided rather than edited, and listed as its own source.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class MiscellaneousSaleEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static int _phoneSuffix;

    // ---- Recording ----

    [Fact]
    public async Task Record_OnAccount_CreatesAnUnpaidSaleInTheMembersDebt()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);

        using var response = await SellAsync(client, token, visit.Id, "  دستکش  ", 2, 150_000m, method: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var sale = await ReadChargeAsync(response);
        sale.Kind.ShouldBe(ServiceChargeKind.Miscellaneous);
        sale.Description.ShouldBe("دستکش");
        sale.Quantity.ShouldBe(2);
        sale.UnitPrice.ShouldBe(150_000m);
        sale.Amount.ShouldBe(300_000m);
        sale.NetPaid.ShouldBe(0m);
        sale.PaymentStatus.ShouldBe(PaymentStatus.Unpaid);
        sale.CanChangeAmount.ShouldBeFalse();

        var debtItem = (await GetDebtOkAsync(client, token, visit.MemberId!.Value)).Items
            .Single(item => item.Id == sale.Id);
        debtItem.ServiceKind.ShouldBe(ServiceChargeKind.Miscellaneous);
        debtItem.Sale.ShouldBe(new MiscellaneousSaleSummary("دستکش", 2, 150_000m));
        debtItem.Outstanding.ShouldBe(300_000m);
    }

    [Fact]
    public async Task Record_PaidByCard_WritesOnePaymentForTheWholeAmount()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);

        var sale = await SellOkAsync(client, token, visit.Id, "حوله", 3, 50_000m, method: "Card");

        sale.NetPaid.ShouldBe(150_000m);
        sale.PaymentStatus.ShouldBe(PaymentStatus.Paid);
        var payment = (await StoredPaymentsAsync(sale.Id)).ShouldHaveSingleItem();
        payment.Kind.ShouldBe(PaymentKind.Payment);
        payment.Amount.ShouldBe(150_000m);
        payment.Method.ShouldBe(PaymentMethod.Card);
        (await GetDebtOkAsync(client, token, visit.MemberId!.Value)).Items
            .ShouldNotContain(item => item.Id == sale.Id);
    }

    /// <summary>Any number per visit (decided with the developer, 1405/07/11), beside one هوازی.</summary>
    [Fact]
    public async Task Record_SeveralOnTheSameVisitBesideCardio_AllStand()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        using (var cardio = await SendAsync(
            client, token, HttpMethod.Post, $"/api/attendance/{visit.Id}/service-charges", new { kind = "Cardio", amount = 40_000m }))
        {
            cardio.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        await SellOkAsync(client, token, visit.Id, "دستکش", 1, 150_000m, method: null);
        await SellOkAsync(client, token, visit.Id, "دستکش", 1, 150_000m, method: null);

        (await GetDebtOkAsync(client, token, visit.MemberId!.Value)).Items
            .Count(item => item.Kind == PaymentTargetKind.ServiceCharge).ShouldBe(3);
    }

    /// <summary>Members only (decided with the developer, 1405/07/11).</summary>
    [Fact]
    public async Task Record_GuestVisit_Returns422ServiceChargesGuestVisit()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await TestGuests.CheckInOkAsync(client, token);

        using var response = await SellAsync(client, token, visit.Id, "دستکش", 1, 150_000m, method: "Cash");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("ServiceCharges.GuestVisit");
    }

    [Fact]
    public async Task Record_ClosedVisit_Returns422ServiceChargesVisitNotOpen()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        using (var checkedOut = await SendAsync(client, token, HttpMethod.Post, $"/api/attendance/{visit.Id}/check-out"))
        {
            checkedOut.EnsureSuccessStatusCode();
        }

        using var response = await SellAsync(client, token, visit.Id, "دستکش", 1, 150_000m, method: null);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("ServiceCharges.VisitNotOpen");
    }

    [Fact]
    public async Task Record_UnknownVisit_Returns404()
    {
        var (client, token) = await StaffClientAsync();

        using var response = await SellAsync(client, token, Guid.CreateVersion7(), "دستکش", 1, 150_000m, method: null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("   ", 1, 10_000, "description", "ServiceCharges.DescriptionRequired")]
    [InlineData("دستکش", 0, 10_000, "quantity", "ServiceCharges.QuantityInvalid")]
    [InlineData("دستکش", 1000, 10_000, "quantity", "ServiceCharges.QuantityInvalid")]
    [InlineData("دستکش", 1, 0, "unitPrice", "ServiceCharges.AmountNotPositive")]
    public async Task Record_InvalidField_Returns400WithItsCode(string description, int quantity, int unitPrice, string field, string code)
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);

        using var response = await SellAsync(client, token, visit.Id, description, quantity, unitPrice, method: null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorCodeAsync(response, field)).ShouldBe(code);
    }

    /// <summary>The هوازی endpoint cannot make a sale without its name and quantity.</summary>
    [Fact]
    public async Task RecordServiceCharge_MiscellaneousKind_Returns400ServiceChargesKindInvalid()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);

        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/attendance/{visit.Id}/service-charges", new { kind = "Miscellaneous", amount = 10_000m });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorCodeAsync(response, "kind")).ShouldBe("ServiceCharges.KindInvalid");
    }

    /// <summary>
    /// The database refuses a sale with no name, or one whose total is not what its quantity and
    /// unit price make, even if the code above it were bypassed.
    /// </summary>
    [Theory]
    [InlineData("description = NULL")]
    [InlineData("amount = 1")]
    [InlineData("quantity = 0, amount = 0")]
    public async Task ServiceCharge_MiscellaneousBrokenByHand_RejectedByACheckConstraint(string change)
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var sale = await SellOkAsync(client, token, visit.Id, "دستکش", 2, 150_000m, method: null);
        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteSqlAsync($"UPDATE service_charges SET {change} WHERE id = '{sale.Id}'"));
        exception.SqlState.ShouldBe("23514");
    }

    // ---- Correcting ----

    [Fact]
    public async Task ChangeAmount_MiscellaneousSale_Returns422NotEditable()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var sale = await SellOkAsync(client, token, visit.Id, "دستکش", 1, 150_000m, method: null);

        using var response = await SendAsync(
            client, token, HttpMethod.Put, $"/api/service-charges/{sale.Id}/amount", new { amount = 100_000m });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("ServiceCharges.MiscellaneousNotEditable");
        (await StoredChargeAsync(sale.Id)).Amount.ShouldBe(150_000m);
    }

    [Fact]
    public async Task Void_PaidSale_RefundsTheMoneyTheWayItCame()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var sale = await SellOkAsync(client, token, visit.Id, "دستکش", 1, 150_000m, method: "Cash");

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
        var ticked = await SellOkAsync(client, token, visit.Id, "دستکش", 1, 150_000m, method: "Card");
        var kept = await SellOkAsync(client, token, visit.Id, "حوله", 1, 50_000m, method: null);

        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/attendance/{visit.Id}/cancel", CancelCheckInBody.VoidMiscellaneous(ticked.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAttendanceAsync(response)).ServiceCharges.Select(charge => charge.Id).ShouldBe([kept.Id]);
        var voided = await StoredChargeAsync(ticked.Id);
        voided.VoidReason.ShouldBe("The check-in was cancelled.");
        (await StoredPaymentsAsync(ticked.Id)).Sum(p => p.Kind == PaymentKind.Payment ? p.Amount : -p.Amount).ShouldBe(0m);
        (await StoredChargeAsync(kept.Id)).IsVoided.ShouldBeFalse();
    }

    [Fact]
    public async Task CancelCheckIn_AnotherVisitsSale_Returns422AndCancelsNothing()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var other = await CheckedInMemberAsync(client, token, lockerNumber: 2);
        var othersSale = await SellOkAsync(client, token, other.Id, "دستکش", 1, 150_000m, method: null);

        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/attendance/{visit.Id}/cancel", CancelCheckInBody.VoidMiscellaneous(othersSale.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.MiscellaneousSaleNotOnVisit");
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
        var sale = await SellOkAsync(client, token, visit.Id, "دستکش", 1, 150_000m, method: null);
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

    /// <summary>The history lists هوازی and متفرقه as two sources (decided with the developer, 1405/07/11).</summary>
    [Fact]
    public async Task ListPayments_MiscellaneousSource_ListsOnlyTheSalesPayments()
    {
        var (client, token) = await OwnerClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var sale = await SellOkAsync(client, token, visit.Id, "دستکش", 1, 150_000m, method: "Cash");
        var cardio = await SendAsync(
            client, token, HttpMethod.Post, $"/api/attendance/{visit.Id}/service-charges", new { kind = "Cardio", amount = 40_000m });
        var cardioId = (await ReadChargeAsync(cardio)).Id;
        cardio.Dispose();
        using (var paid = await SendAsync(
            client, token, HttpMethod.Post, $"/api/service-charges/{cardioId}/payments", new { amount = 40_000m, method = "Cash" }))
        {
            paid.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        using var response = await SendAsync(
            client, token, HttpMethod.Get, "/api/payments?source=ServiceCharge&serviceKind=Miscellaneous");

        response.EnsureSuccessStatusCode();
        var page = (await response.Content.ReadFromJsonAsync<PagedResponse<HistoryPaymentResponse>>(
            TestContext.Current.CancellationToken)).ShouldNotBeNull();
        var row = page.Items.ShouldHaveSingleItem();
        row.TargetId.ShouldBe(sale.Id);
        row.ServiceKind.ShouldBe(ServiceChargeKind.Miscellaneous);
        row.ServiceDescription.ShouldBe("دستکش");
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

    private static Task<HttpResponseMessage> SellAsync(
        HttpClient client, string token, Guid attendanceId, string description, int quantity, decimal unitPrice, string? method) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/attendance/{attendanceId}/service-charges/miscellaneous",
            new { description, quantity, unitPrice, method });

    private static async Task<ServiceChargeResponse> SellOkAsync(
        HttpClient client, string token, Guid attendanceId, string description, int quantity, decimal unitPrice, string? method)
    {
        using var response = await SellAsync(client, token, attendanceId, description, quantity, unitPrice, method);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await ReadChargeAsync(response);
    }

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
